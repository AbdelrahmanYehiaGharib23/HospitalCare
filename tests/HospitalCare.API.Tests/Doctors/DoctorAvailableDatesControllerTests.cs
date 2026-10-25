using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HospitalCare.API.Tests.Infrastructure;
using HospitalCare.Application.Common.Constants;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Common.Models;
using HospitalCare.Application.Features.DoctorSchedules.DTOs;
using HospitalCare.Domain.Entities;
using HospitalCare.Domain.Enums;
using HospitalCare.Infrastructure.Persistence.DbInitializer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalCare.API.Tests.Doctors;

public sealed class DoctorAvailableDatesControllerTests
{
    [Fact]
    public async Task GetAvailableDates_PatientRequest_ReturnsDatesWithinCapacity()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        var from = GetFutureDate(factory.Services);
        var data = await SeedAvailabilityDataAsync(factory, from);
        var token = await RegisterPatientAsync(client);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        using var thirtyMinuteResponse = await client.GetAsync(
            BuildAvailableDatesUrl(
                data.DoctorId,
                data.ThirtyMinuteAppointmentTypeId,
                from,
                from.AddDays(7)),
            TestContext.Current.CancellationToken);
        var thirtyMinuteDates = await thirtyMinuteResponse.Content
            .ReadFromJsonAsync<DoctorAvailableDateDto[]>(
                TestContext.Current.CancellationToken);

        using var fortyFiveMinuteResponse = await client.GetAsync(
            BuildAvailableDatesUrl(
                data.DoctorId,
                data.FortyFiveMinuteAppointmentTypeId,
                from,
                from.AddDays(7)),
            TestContext.Current.CancellationToken);
        var fortyFiveMinuteDates = await fortyFiveMinuteResponse.Content
            .ReadFromJsonAsync<DoctorAvailableDateDto[]>(
                TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, thirtyMinuteResponse.StatusCode);
        Assert.NotNull(thirtyMinuteDates);
        Assert.Equal(
            [from, from.AddDays(7)],
            thirtyMinuteDates.Select(item => item.Date).ToArray());
        Assert.Equal(HttpStatusCode.OK, fortyFiveMinuteResponse.StatusCode);
        Assert.NotNull(fortyFiveMinuteDates);
        Assert.Equal(
            [from.AddDays(7)],
            fortyFiveMinuteDates.Select(item => item.Date).ToArray());
    }

    [Fact]
    public async Task GetAvailableDates_PendingDeactivationDoctor_ReturnsNoDates()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        var from = GetFutureDate(factory.Services);
        var data = await SeedAvailabilityDataAsync(
            factory,
            from,
            pendingDeactivation: true);
        var token = await RegisterPatientAsync(client);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.GetAsync(
            BuildAvailableDatesUrl(
                data.DoctorId,
                data.ThirtyMinuteAppointmentTypeId,
                from,
                from),
            TestContext.Current.CancellationToken);
        var dates = await response.Content
            .ReadFromJsonAsync<DoctorAvailableDateDto[]>(
                TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(dates);
        Assert.Empty(dates);
    }

    [Fact]
    public async Task GetAvailableDates_InactiveDoctor_ReturnsNotFound()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        var from = GetFutureDate(factory.Services);
        var data = await SeedAvailabilityDataAsync(
            factory,
            from,
            isActive: false);
        var token = await RegisterPatientAsync(client);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.GetAsync(
            BuildAvailableDatesUrl(
                data.DoctorId,
                data.ThirtyMinuteAppointmentTypeId,
                from,
                from),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAvailableDates_DoctorRequest_ReturnsForbidden()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        var from = GetFutureDate(factory.Services);
        var data = await SeedAvailabilityDataAsync(factory, from);
        using var scope = factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider
            .GetRequiredService<IJwtTokenService>();
        var token = tokenService.GenerateToken(
            "doctor-user",
            "doctor@example.com",
            [Roles.Doctor],
            doctorId: data.DoctorId.ToString()).Token;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.GetAsync(
            BuildAvailableDatesUrl(
                data.DoctorId,
                data.ThirtyMinuteAppointmentTypeId,
                from,
                from),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<SeededAvailability> SeedAvailabilityDataAsync(
        HospitalCareApiFactory factory,
        DateOnly date,
        bool isActive = true,
        bool pendingDeactivation = false)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        var department = new Department { Name = $"Department-{Guid.NewGuid():N}" };
        var doctor = new Doctor
        {
            Name = $"Doctor-{Guid.NewGuid():N}",
            Department = department,
            IsActive = isActive,
            PendingDeactivation = pendingDeactivation
        };
        var thirtyMinuteType = new AppointmentType
        {
            Name = $"Thirty minute-{Guid.NewGuid():N}",
            DurationMinutes = 30,
            BookingFee = 100
        };
        var fortyFiveMinuteType = new AppointmentType
        {
            Name = $"Forty five minute-{Guid.NewGuid():N}",
            DurationMinutes = 45,
            BookingFee = 120
        };
        var patient = new Patient
        {
            UserId = $"patient-{Guid.NewGuid():N}",
            Name = "Test Patient",
            Email = $"patient-{Guid.NewGuid():N}@example.com",
            Phone = "01000000000",
            DateOfBirth = new DateOnly(1980, 1, 1)
        };

        doctor.Schedules.Add(new DoctorSchedule
        {
            DayOfWeek = date.DayOfWeek,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 0)
        });

        dbContext.Doctors.Add(doctor);
        dbContext.AppointmentTypes.AddRange(
            thirtyMinuteType,
            fortyFiveMinuteType);
        dbContext.Patients.Add(patient);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        dbContext.Appointments.AddRange(
            Appointment(
                doctor.Id,
                patient.Id,
                thirtyMinuteType.Id,
                date,
                1,
                AppointmentStatus.Queued),
            Appointment(
                doctor.Id,
                patient.Id,
                thirtyMinuteType.Id,
                date,
                2,
                AppointmentStatus.Completed),
            Appointment(
                doctor.Id,
                patient.Id,
                thirtyMinuteType.Id,
                date,
                3,
                AppointmentStatus.Cancelled),
            Appointment(
                doctor.Id,
                patient.Id,
                thirtyMinuteType.Id,
                date,
                4,
                AppointmentStatus.NoShowPenalized));
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        return new SeededAvailability(
            doctor.Id,
            thirtyMinuteType.Id,
            fortyFiveMinuteType.Id);
    }

    private static Appointment Appointment(
        int doctorId,
        int patientId,
        int appointmentTypeId,
        DateOnly date,
        int queuePosition,
        AppointmentStatus status)
    {
        return new Appointment
        {
            DoctorId = doctorId,
            PatientId = patientId,
            AppointmentTypeId = appointmentTypeId,
            AppointmentDate = date,
            QueuePosition = queuePosition,
            Status = status
        };
    }

    private static DateOnly GetFutureDate(IServiceProvider services)
    {
        var timeProvider = services.GetRequiredService<TimeProvider>();
        var timeZone = services.GetRequiredService<TimeZoneInfo>();
        var localNow = TimeZoneInfo.ConvertTime(
            timeProvider.GetUtcNow(),
            timeZone);

        return DateOnly.FromDateTime(localNow.DateTime).AddDays(3);
    }

    private static string BuildAvailableDatesUrl(
        int doctorId,
        int appointmentTypeId,
        DateOnly from,
        DateOnly to)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "/api/doctors/{0}/available-dates?appointmentTypeId={1}&from={2:yyyy-MM-dd}&to={3:yyyy-MM-dd}",
            doctorId,
            appointmentTypeId,
            from,
            to);
    }

    private static async Task<string> RegisterPatientAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                Name = "Test Patient",
                Email = $"patient-{Guid.NewGuid():N}@example.com",
                Password = "Password123!",
                Phone = "01000000000",
                DateOfBirth = new DateOnly(1980, 1, 1)
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = await response.Content
            .ReadFromJsonAsync<JwtTokenResult>(
                TestContext.Current.CancellationToken);

        Assert.NotNull(token);
        return token.Token;
    }

    private sealed record SeededAvailability(
        int DoctorId,
        int ThirtyMinuteAppointmentTypeId,
        int FortyFiveMinuteAppointmentTypeId);
}
