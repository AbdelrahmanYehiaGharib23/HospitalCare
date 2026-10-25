using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HospitalCare.API.Tests.Infrastructure;
using HospitalCare.Application.Authentication;
using HospitalCare.Application.Common.Models;
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
    public async Task InitiatePayment_PatientWithAvailableCapacity_ReturnsCheckout()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;
        const string email = "payment-patient@example.com";
        var token = await RegisterPatientAsync(client, email);
        var booking = await SeedBookingDataAsync(factory, email);
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
        Assert.Empty(await dbContext.Appointments.ToListAsync(cancellationToken));
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

    private static async Task<(int DoctorId, int AppointmentTypeId, int PatientId, DateOnly Date)>
        SeedBookingDataAsync(
            HospitalCareApiFactory factory,
            string patientEmail)
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

        return (doctor.Id, appointmentType.Id, patient.Id, date);
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
}
