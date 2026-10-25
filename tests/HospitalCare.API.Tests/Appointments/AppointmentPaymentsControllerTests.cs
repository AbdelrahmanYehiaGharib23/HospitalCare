using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using HospitalCare.API.Tests.Infrastructure;
using HospitalCare.Application.Authentication;
using HospitalCare.Application.Common.Constants;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Common.Models;
using HospitalCare.Application.Features.Appointments.DTOs;
using HospitalCare.Application.Features.Payments.DTOs;
using HospitalCare.Domain.Entities;
using HospitalCare.Domain.Enums;
using HospitalCare.Infrastructure.Persistence.DbInitializer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalCare.API.Tests.Appointments;

public sealed class AppointmentPaymentsControllerTests
{
    [Fact]
    public async Task InitiatePayment_ThenConfirmedWebhook_CreatesQueuedAppointment()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;
        const string email = "payment-patient@example.com";
        var token = await RegisterPatientAsync(client, email);
        var booking = await SeedBookingDataAsync(
            factory,
            email,
            cancelledQueuePosition: 4);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.PostAsJsonAsync(
            "/api/appointments/payments",
            new
            {
                booking.DoctorId,
                booking.AppointmentTypeId,
                RequestedDate = booking.Date
            },
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var checkout = await response.Content
            .ReadFromJsonAsync<InitiatedAppointmentPaymentDto>(
                cancellationToken);
        Assert.NotNull(checkout);
        Assert.Equal(100m, checkout.Amount);
        Assert.Equal("EGP", checkout.Currency);
        Assert.StartsWith(
            "https://fake-payment.invalid/checkout/fake-",
            checkout.CheckoutUrl);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        var payment = await dbContext.PaymentTransactions
            .SingleAsync(cancellationToken);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Equal($"fake-{payment.Id}", payment.GatewayReference);
        Assert.Equal(booking.PatientId, payment.PatientId);
        var appointmentsBeforeConfirmation = await dbContext.Appointments
            .ToListAsync(cancellationToken);
        var existingAppointment = Assert.Single(appointmentsBeforeConfirmation);
        Assert.Equal(
            AppointmentStatus.Cancelled,
            existingAppointment.Status);

        using var webhookResponse = await SendWebhookAsync(
            client,
            payment.GatewayReference!,
            payment.Amount,
            "Succeeded");
        Assert.Equal(HttpStatusCode.OK, webhookResponse.StatusCode);
        var webhookResult = await webhookResponse.Content
            .ReadFromJsonAsync<PaymentWebhookResultDto>(cancellationToken);
        Assert.NotNull(webhookResult);
        Assert.Equal(PaymentStatus.Succeeded, webhookResult.Status);
        Assert.Equal(5, webhookResult.QueuePosition);

        using var duplicateResponse = await SendWebhookAsync(
            client,
            payment.GatewayReference!,
            payment.Amount,
            "Succeeded");
        Assert.Equal(HttpStatusCode.OK, duplicateResponse.StatusCode);
        var duplicateResult = await duplicateResponse.Content
            .ReadFromJsonAsync<PaymentWebhookResultDto>(cancellationToken);
        Assert.NotNull(duplicateResult);
        Assert.Equal(webhookResult.AppointmentId, duplicateResult.AppointmentId);

        Assert.Equal(
            2,
            await dbContext.Appointments.CountAsync(cancellationToken));

        using var myAppointmentsResponse = await client.GetAsync(
            "/api/appointments/me?pageNumber=1&pageSize=10",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, myAppointmentsResponse.StatusCode);
        var myAppointments = await myAppointmentsResponse.Content
            .ReadFromJsonAsync<PagedResult<PatientAppointmentDto>>(
                cancellationToken);
        Assert.NotNull(myAppointments);
        Assert.Equal(2, myAppointments.TotalCount);
        var myAppointment = Assert.Single(
            myAppointments.Items,
            item => item.Id == webhookResult.AppointmentId);
        Assert.Equal(1, myAppointment.QueuePosition);

        using var appointmentResponse = await client.GetAsync(
            $"/api/appointments/{webhookResult.AppointmentId}",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, appointmentResponse.StatusCode);
        var appointment = await appointmentResponse.Content
            .ReadFromJsonAsync<PatientAppointmentDto>(cancellationToken);
        Assert.NotNull(appointment);
        Assert.Equal(myAppointment.Id, appointment.Id);
        Assert.Equal(1, appointment.QueuePosition);

        using var otherPatientClient = factory.CreateClient();
        var otherPatientToken = await RegisterPatientAsync(
            otherPatientClient,
            "other-payment-patient@example.com");
        otherPatientClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", otherPatientToken);
        using var otherPatientResponse = await otherPatientClient.GetAsync(
            $"/api/appointments/{webhookResult.AppointmentId}",
            cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, otherPatientResponse.StatusCode);
        using var otherPatientCancelResponse = await otherPatientClient.PostAsync(
            $"/api/appointments/{webhookResult.AppointmentId}/cancel",
            null,
            cancellationToken);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            otherPatientCancelResponse.StatusCode);

        using var cancelResponse = await client.PostAsync(
            $"/api/appointments/{webhookResult.AppointmentId}/cancel",
            null,
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
        var cancelledAppointment = await cancelResponse.Content
            .ReadFromJsonAsync<CancelledAppointmentDto>(cancellationToken);
        Assert.NotNull(cancelledAppointment);
        Assert.Equal(AppointmentStatus.Cancelled, cancelledAppointment.Status);
        Assert.Equal(PaymentStatus.Refunded, cancelledAppointment.PaymentStatus);

        using var duplicateCancelResponse = await client.PostAsync(
            $"/api/appointments/{webhookResult.AppointmentId}/cancel",
            null,
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, duplicateCancelResponse.StatusCode);

        using var cancelledListResponse = await client.GetAsync(
            "/api/appointments/me",
            cancellationToken);
        var cancelledAppointments = await cancelledListResponse.Content
            .ReadFromJsonAsync<PagedResult<PatientAppointmentDto>>(
                cancellationToken);
        Assert.NotNull(cancelledAppointments);
        Assert.All(
            cancelledAppointments.Items,
            item => Assert.Null(item.QueuePosition));
    }

    [Fact]
    public async Task HandleWebhook_InvalidSignature_ReturnsBadRequest()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        const string payload = "{\"gatewayReference\":\"fake-1\",\"status\":\"Succeeded\",\"amount\":100,\"currency\":\"EGP\"}";
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/payments/webhook")
        {
            Content = new StringContent(
                payload,
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Add("X-Payment-Signature", "00");

        using var response = await client.SendAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task HandlePaymobWebhook_InvalidSignature_ReturnsBadRequest()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        const string payload = "{\"gatewayReference\":\"fake-1\",\"status\":\"Succeeded\",\"amount\":100,\"currency\":\"EGP\"}";
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/payments/paymob/webhook?hmac=00")
        {
            Content = new StringContent(
                payload,
                Encoding.UTF8,
                "application/json")
        };
        using var response = await client.SendAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ConfirmPayment_FailedGatewayEvent_DoesNotCreateAppointment()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        const string email = "failed-payment-patient@example.com";
        var token = await RegisterPatientAsync(client, email);
        var booking = await SeedBookingDataAsync(factory, email);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        using var checkoutResponse = await client.PostAsJsonAsync(
            "/api/appointments/payments",
            new
            {
                booking.DoctorId,
                booking.AppointmentTypeId,
                RequestedDate = booking.Date
            },
            TestContext.Current.CancellationToken);
        var checkout = await checkoutResponse.Content
            .ReadFromJsonAsync<InitiatedAppointmentPaymentDto>(
                TestContext.Current.CancellationToken);
        Assert.NotNull(checkout);

        using var webhookResponse = await SendWebhookAsync(
            client,
            $"fake-{checkout.PaymentTransactionId}",
            checkout.Amount,
            "Failed",
            useQueryHmac: true);
        Assert.Equal(HttpStatusCode.OK, webhookResponse.StatusCode);
        var result = await webhookResponse.Content
            .ReadFromJsonAsync<PaymentWebhookResultDto>(
                TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(PaymentStatus.Failed, result.Status);
        Assert.Equal(0, await CountAppointmentsAsync(
            factory,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ConfirmPayment_PendingGatewayEvent_KeepsPaymentPending()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        const string email = "pending-payment-patient@example.com";
        var token = await RegisterPatientAsync(client, email);
        var booking = await SeedBookingDataAsync(factory, email);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        using var checkoutResponse = await client.PostAsJsonAsync(
            "/api/appointments/payments",
            new
            {
                booking.DoctorId,
                booking.AppointmentTypeId,
                RequestedDate = booking.Date
            },
            TestContext.Current.CancellationToken);
        var checkout = await checkoutResponse.Content
            .ReadFromJsonAsync<InitiatedAppointmentPaymentDto>(
                TestContext.Current.CancellationToken);
        Assert.NotNull(checkout);

        using var webhookResponse = await SendWebhookAsync(
            client,
            $"fake-{checkout.PaymentTransactionId}",
            checkout.Amount,
            "Pending");

        Assert.Equal(HttpStatusCode.OK, webhookResponse.StatusCode);
        var result = await webhookResponse.Content
            .ReadFromJsonAsync<PaymentWebhookResultDto>(
                TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(PaymentStatus.Pending, result.Status);
        Assert.Equal(0, await CountAppointmentsAsync(
            factory,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ConfirmPayment_CapacityFilledAfterCheckout_RefundsPayment()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;
        const string email = "capacity-race-patient@example.com";
        var token = await RegisterPatientAsync(client, email);
        var booking = await SeedBookingDataAsync(factory, email);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        using var checkoutResponse = await client.PostAsJsonAsync(
            "/api/appointments/payments",
            new
            {
                booking.DoctorId,
                booking.AppointmentTypeId,
                RequestedDate = booking.Date
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, checkoutResponse.StatusCode);
        var checkout = await checkoutResponse.Content
            .ReadFromJsonAsync<InitiatedAppointmentPaymentDto>(cancellationToken);
        Assert.NotNull(checkout);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();
            dbContext.Appointments.AddRange(
                CreateAppointment(booking, 1),
                CreateAppointment(booking, 2));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        using var webhookResponse = await SendWebhookAsync(
            client,
            $"fake-{checkout.PaymentTransactionId}",
            checkout.Amount,
            "Succeeded");
        Assert.Equal(HttpStatusCode.OK, webhookResponse.StatusCode);
        var webhookResult = await webhookResponse.Content
            .ReadFromJsonAsync<PaymentWebhookResultDto>(cancellationToken);
        Assert.NotNull(webhookResult);
        Assert.Equal(PaymentStatus.Refunded, webhookResult.Status);
        Assert.Null(webhookResult.AppointmentId);

        using var duplicateResponse = await SendWebhookAsync(
            client,
            $"fake-{checkout.PaymentTransactionId}",
            checkout.Amount,
            "Succeeded");
        Assert.Equal(HttpStatusCode.OK, duplicateResponse.StatusCode);
        Assert.Equal(
            2,
            await CountAppointmentsAsync(factory, cancellationToken));
        Assert.Equal(
            PaymentStatus.Refunded,
            await GetPaymentStatusAsync(
                factory,
                checkout.PaymentTransactionId,
                cancellationToken));
    }

    [Theory]
    [InlineData("Doctor")]
    [InlineData("Department")]
    public async Task ConfirmPayment_WhenDoctorOrDepartmentBecomesPendingAfterCheckout_RefundsAndDoesNotQueue(
        string deactivationTarget)
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;
        var email = deactivationTarget == "Department"
            ? "pending-department-payment@example.com"
            : "pending-doctor-payment@example.com";
        var token = await RegisterPatientAsync(client, email);
        var booking = await SeedBookingDataAsync(
            factory,
            email,
            includeQueuedAppointment: true);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        using var checkoutResponse = await client.PostAsJsonAsync(
            "/api/appointments/payments",
            new
            {
                booking.DoctorId,
                booking.AppointmentTypeId,
                RequestedDate = booking.Date
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, checkoutResponse.StatusCode);
        var checkout = await checkoutResponse.Content
            .ReadFromJsonAsync<InitiatedAppointmentPaymentDto>(cancellationToken);
        Assert.NotNull(checkout);

        AuthenticateAsAdmin(factory, client);
        var deactivatePath = deactivationTarget == "Department"
            ? $"/api/departments/{booking.DepartmentId}/deactivate"
            : $"/api/doctors/{booking.DoctorId}/deactivate";
        using var deactivateResponse = await client.PostAsync(
            deactivatePath,
            content: null,
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);
        client.DefaultRequestHeaders.Authorization = null;

        using var webhookResponse = await SendWebhookAsync(
            client,
            $"fake-{checkout.PaymentTransactionId}",
            checkout.Amount,
            "Succeeded");
        Assert.Equal(HttpStatusCode.OK, webhookResponse.StatusCode);
        var webhookResult = await webhookResponse.Content
            .ReadFromJsonAsync<PaymentWebhookResultDto>(cancellationToken);
        Assert.NotNull(webhookResult);
        Assert.Equal(PaymentStatus.Refunded, webhookResult.Status);
        Assert.Null(webhookResult.AppointmentId);
        Assert.Equal(1, await CountAppointmentsAsync(factory, cancellationToken));
        Assert.Equal(
            PaymentStatus.Refunded,
            await GetPaymentStatusAsync(
                factory,
                checkout.PaymentTransactionId,
                cancellationToken));
    }

    [Fact]
    public async Task RefundPending_DuplicateWebhookAndCancellation_DoNotRetryProviderRefund()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;
        const string email = "pending-refund-patient@example.com";
        var token = await RegisterPatientAsync(client, email);
        var booking = await SeedBookingDataAsync(factory, email);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        using var checkoutResponse = await client.PostAsJsonAsync(
            "/api/appointments/payments",
            new
            {
                booking.DoctorId,
                booking.AppointmentTypeId,
                RequestedDate = booking.Date
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, checkoutResponse.StatusCode);
        var checkout = await checkoutResponse.Content
            .ReadFromJsonAsync<InitiatedAppointmentPaymentDto>(
                cancellationToken);
        Assert.NotNull(checkout);

        using var paymentResponse = await SendWebhookAsync(
            client,
            $"fake-{checkout.PaymentTransactionId}",
            checkout.Amount,
            "Succeeded");
        Assert.Equal(HttpStatusCode.OK, paymentResponse.StatusCode);
        var paymentResult = await paymentResponse.Content
            .ReadFromJsonAsync<PaymentWebhookResultDto>(cancellationToken);
        Assert.NotNull(paymentResult);
        Assert.NotNull(paymentResult.AppointmentId);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();
            var payment = await dbContext.PaymentTransactions
                .SingleAsync(
                    transaction => transaction.Id == checkout.PaymentTransactionId,
                    cancellationToken);
            var appointment = await dbContext.Appointments
                .SingleAsync(
                    item => item.Id == paymentResult.AppointmentId,
                    cancellationToken);
            payment.Status = PaymentStatus.RefundPending;
            appointment.Status = AppointmentStatus.Cancelled;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        using var duplicateWebhookResponse = await SendWebhookAsync(
            client,
            $"fake-{checkout.PaymentTransactionId}",
            checkout.Amount,
            "Succeeded");
        Assert.Equal(HttpStatusCode.OK, duplicateWebhookResponse.StatusCode);
        var duplicateWebhookResult = await duplicateWebhookResponse.Content
            .ReadFromJsonAsync<PaymentWebhookResultDto>(cancellationToken);
        Assert.NotNull(duplicateWebhookResult);
        Assert.Equal(PaymentStatus.RefundPending, duplicateWebhookResult.Status);

        using var duplicateCancelResponse = await client.PostAsync(
            $"/api/appointments/{paymentResult.AppointmentId}/cancel",
            content: null,
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, duplicateCancelResponse.StatusCode);
        var cancelledAppointment = await duplicateCancelResponse.Content
            .ReadFromJsonAsync<CancelledAppointmentDto>(cancellationToken);
        Assert.NotNull(cancelledAppointment);
        Assert.Equal(PaymentStatus.RefundPending, cancelledAppointment.PaymentStatus);
    }

    [Fact]
    public async Task InitiatePayment_AnonymousUser_ReturnsUnauthorized()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/appointments/payments",
            new
            {
                DoctorId = 1,
                AppointmentTypeId = 1,
                RequestedDate = new DateOnly(2030, 1, 1)
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<(
        int DoctorId,
        int DepartmentId,
        int AppointmentTypeId,
        int PatientId,
        DateOnly Date)>
        SeedBookingDataAsync(
            HospitalCareApiFactory factory,
            string patientEmail,
            int? cancelledQueuePosition = null,
            bool includeQueuedAppointment = false)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        var patient = await dbContext.Patients
            .SingleAsync(
                candidate => candidate.Email == patientEmail,
                TestContext.Current.CancellationToken);
        var timeProvider = scope.ServiceProvider
            .GetRequiredService<TimeProvider>();
        var timeZone = scope.ServiceProvider
            .GetRequiredService<TimeZoneInfo>();
        var hospitalLocalNow = TimeZoneInfo.ConvertTime(
            timeProvider.GetUtcNow(),
            timeZone);
        var date = DateOnly.FromDateTime(hospitalLocalNow.DateTime).AddDays(5);
        var department = new Department
        {
            Name = $"Department-{Guid.NewGuid():N}"
        };
        var doctor = new Doctor
        {
            Name = $"Doctor-{Guid.NewGuid():N}",
            Department = department
        };
        var appointmentType = new AppointmentType
        {
            Name = $"Consultation-{Guid.NewGuid():N}",
            DurationMinutes = 30,
            BookingFee = 100
        };
        doctor.Schedules.Add(new DoctorSchedule
        {
            DayOfWeek = date.DayOfWeek,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 0)
        });

        dbContext.AddRange(doctor, appointmentType);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var booking = (
            DoctorId: doctor.Id,
            DepartmentId: department.Id,
            AppointmentTypeId: appointmentType.Id,
            PatientId: patient.Id,
            Date: date);

        if (cancelledQueuePosition.HasValue)
        {
            dbContext.Appointments.Add(new Appointment
            {
                DoctorId = doctor.Id,
                AppointmentTypeId = appointmentType.Id,
                PatientId = patient.Id,
                AppointmentDate = date,
                QueuePosition = cancelledQueuePosition.Value,
                Status = AppointmentStatus.Cancelled
            });
        }

        if (includeQueuedAppointment)
        {
            dbContext.Appointments.Add(CreateAppointment(booking, 1));
        }

        if (cancelledQueuePosition.HasValue || includeQueuedAppointment)
        {
            await dbContext.SaveChangesAsync(
                TestContext.Current.CancellationToken);
        }

        return booking;
    }

    private static async Task<string> RegisterPatientAsync(
        HttpClient client,
        string email)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterPatientCommand(
                "Payment Patient",
                email,
                "Password123!",
                "01000000000",
                new DateOnly(1990, 1, 1)),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = await response.Content
            .ReadFromJsonAsync<JwtTokenResult>(
                TestContext.Current.CancellationToken);
        Assert.NotNull(token);
        return token.Token;
    }

    private static Appointment CreateAppointment(
        (
            int DoctorId,
            int DepartmentId,
            int AppointmentTypeId,
            int PatientId,
            DateOnly Date) booking,
        int queuePosition)
    {
        return new Appointment
        {
            DoctorId = booking.DoctorId,
            AppointmentTypeId = booking.AppointmentTypeId,
            PatientId = booking.PatientId,
            AppointmentDate = booking.Date,
            QueuePosition = queuePosition
        };
    }

    private static void AuthenticateAsAdmin(
        HospitalCareApiFactory factory,
        HttpClient client)
    {
        using var scope = factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider
            .GetRequiredService<IJwtTokenService>();
        var token = tokenService.GenerateToken(
            "admin-user",
            "admin@example.com",
            [Roles.Admin]);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.Token);
    }

    private static async Task<HttpResponseMessage> SendWebhookAsync(
        HttpClient client,
        string gatewayReference,
        decimal amount,
        string status,
        bool useQueryHmac = false)
    {
        var payload =
            $"{{\"gatewayReference\":\"{gatewayReference}\",\"status\":\"{status}\",\"amount\":{amount},\"currency\":\"EGP\"}}";
        var key = Encoding.UTF8.GetBytes(
            "HospitalCare integration test webhook signing secret");
        var signature = Convert.ToHexString(
            HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(payload)));
        var requestPath = useQueryHmac
            ? $"/api/payments/webhook?hmac={Uri.EscapeDataString(signature)}"
            : "/api/payments/webhook";
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            requestPath)
        {
            Content = new StringContent(
                payload,
                Encoding.UTF8,
                "application/json")
        };
        if (!useQueryHmac)
        {
            request.Headers.Add("X-Payment-Signature", signature);
        }
        return await client.SendAsync(
            request,
            TestContext.Current.CancellationToken);
    }

    private static async Task<int> CountAppointmentsAsync(
        HospitalCareApiFactory factory,
        CancellationToken cancellationToken)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        return await dbContext.Appointments.CountAsync(cancellationToken);
    }

    private static async Task<PaymentStatus> GetPaymentStatusAsync(
        HospitalCareApiFactory factory,
        int paymentTransactionId,
        CancellationToken cancellationToken)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        return (await dbContext.PaymentTransactions
                .SingleAsync(
                    payment => payment.Id == paymentTransactionId,
                    cancellationToken))
            .Status;
    }
}
