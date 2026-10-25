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
using HospitalCare.Application.Features.DoctorQueues.DTOs;
using HospitalCare.Application.Features.DoctorSchedules.DTOs;
using HospitalCare.Application.Features.Payments.DTOs;
using HospitalCare.Domain.Entities;
using HospitalCare.Domain.Enums;
using HospitalCare.Infrastructure.Persistence.DbInitializer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalCare.API.Tests.Appointments;

public sealed class AppointmentBookingFlowTests
{
    [Fact]
    public async Task RegisterToCompletedAppointment_MainFlow_Succeeds()
    {
        using var factory = new HospitalCareApiFactory(
            new FixedTimeProvider(
                new DateTimeOffset(2030, 1, 10, 8, 0, 0, TimeSpan.Zero)));
        using var client = factory.CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;
        const string email = "booking-flow@example.com";
        const string password = "Password1!";

        using var registrationResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterPatientCommand(
                "Booking Flow Patient",
                email,
                password,
                "01000000000",
                new DateOnly(1990, 1, 1)),
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, registrationResponse.StatusCode);

        using var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginCommand(email, password),
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var token = await loginResponse.Content
            .ReadFromJsonAsync<JwtTokenResult>(cancellationToken);
        Assert.NotNull(token);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.Token);

        var booking = await SeedBookingDataAsync(factory);
        using var availabilityResponse = await client.GetAsync(
            $"/api/doctors/{booking.DoctorId}/available-dates" +
            $"?appointmentTypeId={booking.AppointmentTypeId}" +
            $"&from={booking.Date:yyyy-MM-dd}&to={booking.Date:yyyy-MM-dd}",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, availabilityResponse.StatusCode);
        var availableDates = await availabilityResponse.Content
            .ReadFromJsonAsync<DoctorAvailableDateDto[]>(cancellationToken);
        Assert.NotNull(availableDates);
        Assert.Contains(availableDates, date => date.Date == booking.Date);

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

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        var payment = await dbContext.PaymentTransactions
            .AsNoTracking()
            .SingleAsync(
                candidate => candidate.Id == checkout.PaymentTransactionId,
                cancellationToken);
        using var webhookResponse = await SendWebhookAsync(
            client,
            payment.GatewayReference!,
            payment.Amount,
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, webhookResponse.StatusCode);
        var webhookResult = await webhookResponse.Content
            .ReadFromJsonAsync<PaymentWebhookResultDto>(cancellationToken);
        Assert.NotNull(webhookResult);
        Assert.Equal(PaymentStatus.Succeeded, webhookResult.Status);
        Assert.NotNull(webhookResult.AppointmentId);

        Authenticate(factory, client, Roles.Doctor, booking.DoctorId);
        using var queueResponse = await client.GetAsync(
            $"/api/doctors/me/queue?date={booking.Date:yyyy-MM-dd}",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, queueResponse.StatusCode);
        var queue = await queueResponse.Content
            .ReadFromJsonAsync<PagedResult<DoctorQueueItemDto>>(
                cancellationToken);
        Assert.NotNull(queue);
        var queueItem = Assert.Single(queue.Items);
        Assert.Equal(webhookResult.AppointmentId, queueItem.AppointmentId);
        Assert.Equal(1, queueItem.QueuePosition);

        Authenticate(factory, client, Roles.Receptionist);
        using var checkInResponse = await client.PostAsync(
            $"/api/appointments/{webhookResult.AppointmentId}/check-in",
            content: null,
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, checkInResponse.StatusCode);

        Authenticate(factory, client, Roles.Doctor, booking.DoctorId);
        using var callResponse = await client.PostAsync(
            $"/api/doctors/me/queue/call-next?date={booking.Date:yyyy-MM-dd}",
            content: null,
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, callResponse.StatusCode);
        var calledAppointment = await callResponse.Content
            .ReadFromJsonAsync<AppointmentDto>(cancellationToken);
        Assert.NotNull(calledAppointment);
        Assert.Equal(webhookResult.AppointmentId, calledAppointment.Id);

        using var startResponse = await client.PostAsync(
            $"/api/appointments/{calledAppointment.Id}/start",
            content: null,
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);

        using var completeResponse = await client.PostAsJsonAsync(
            $"/api/appointments/{calledAppointment.Id}/complete",
            new { Notes = "Consultation completed." },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);
        var completedAppointment = await completeResponse.Content
            .ReadFromJsonAsync<AppointmentDto>(cancellationToken);
        Assert.NotNull(completedAppointment);
        Assert.Equal(AppointmentStatus.Completed, completedAppointment.Status);
        Assert.NotNull(completedAppointment.ActualStartTime);
        Assert.NotNull(completedAppointment.ActualEndTime);
        Assert.Equal("Consultation completed.", completedAppointment.Notes);
    }

    private static async Task<BookingData> SeedBookingDataAsync(
        HospitalCareApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var dbContext = services.GetRequiredService<ApplicationDbContext>();
        var timeProvider = services.GetRequiredService<TimeProvider>();
        var timeZone = services.GetRequiredService<TimeZoneInfo>();
        var hospitalLocalNow = TimeZoneInfo.ConvertTime(
            timeProvider.GetUtcNow(),
            timeZone);
        var date = DateOnly.FromDateTime(hospitalLocalNow.DateTime);
        var department = new Department
        {
            Name = $"Flow Department-{Guid.NewGuid():N}"
        };
        var doctor = new Doctor
        {
            Name = $"Flow Doctor-{Guid.NewGuid():N}",
            Department = department
        };
        var appointmentType = await dbContext.AppointmentTypes
            .AsNoTracking()
            .SingleAsync(
                candidate => candidate.Name == "Consultation",
                TestContext.Current.CancellationToken);
        doctor.Schedules.Add(new DoctorSchedule
        {
            DayOfWeek = date.DayOfWeek,
            StartTime = TimeOnly.MinValue,
            EndTime = new TimeOnly(23, 59)
        });
        dbContext.AddRange(department, doctor);
        await dbContext.SaveChangesAsync(
            TestContext.Current.CancellationToken);

        return new BookingData(doctor.Id, appointmentType.Id, date);
    }

    private static void Authenticate(
        HospitalCareApiFactory factory,
        HttpClient client,
        string role,
        int? doctorId = null)
    {
        using var scope = factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider
            .GetRequiredService<IJwtTokenService>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                tokenService.GenerateToken(
                    $"{role.ToLowerInvariant()}-user",
                    $"{role.ToLowerInvariant()}@example.com",
                    [role],
                    doctorId: doctorId?.ToString()).Token);
    }

    private static async Task<HttpResponseMessage> SendWebhookAsync(
        HttpClient client,
        string gatewayReference,
        decimal amount,
        CancellationToken cancellationToken)
    {
        var payload =
            $"{{\"gatewayReference\":\"{gatewayReference}\",\"status\":\"Succeeded\",\"amount\":{amount},\"currency\":\"EGP\"}}";
        var key = Encoding.UTF8.GetBytes(
            "HospitalCare integration test webhook signing secret");
        var signature = Convert.ToHexString(
            HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(payload)));
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/payments/webhook")
        {
            Content = new StringContent(
                payload,
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Add("X-Payment-Signature", signature);
        return await client.SendAsync(request, cancellationToken);
    }

    private sealed record BookingData(
        int DoctorId,
        int AppointmentTypeId,
        DateOnly Date);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
