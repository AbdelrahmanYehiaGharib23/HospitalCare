using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HospitalCare.API.Tests.Infrastructure;
using HospitalCare.Application.Authentication;
using HospitalCare.Application.Common.Constants;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Common.Models;
using HospitalCare.Application.Features.DoctorQueues.DTOs;
using HospitalCare.Domain.Entities;
using HospitalCare.Domain.Enums;
using HospitalCare.Infrastructure.Persistence.DbInitializer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalCare.API.Tests.Doctors;

public sealed class DoctorQueueControllerTests
{
    [Fact]
    public async Task GetQueueForDate_DoctorOwnsQueue_ReturnsEstimates()
    {
        using var factory = new HospitalCareApiFactory(
            new FixedTimeProvider(
                new DateTimeOffset(2030, 1, 10, 5, 0, 0, TimeSpan.Zero)));
        using var client = factory.CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;
        const string patientEmail = "queue-patient@example.com";
        await RegisterPatientAsync(client, patientEmail);
        var queue = await SeedQueueAsync(factory, patientEmail);
        var token = CreateDoctorToken(factory, queue.DoctorId);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.GetAsync(
            $"/api/doctors/me/queue?date={queue.Date:yyyy-MM-dd}&doctorId={queue.DoctorId + 1}",
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content
            .ReadFromJsonAsync<PagedResult<DoctorQueueItemDto>>(
                cancellationToken);
        Assert.NotNull(result);
        var item = Assert.Single(result.Items);
        Assert.Equal(queue.QueuedAppointmentId, item.AppointmentId);
        Assert.Equal(3, item.QueuePosition);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        var completedAppointment = await dbContext.Appointments.SingleAsync(
            appointment =>
                appointment.DoctorId == queue.DoctorId &&
                appointment.Status == AppointmentStatus.Completed,
            cancellationToken);
        var appointmentType = await dbContext.AppointmentTypes.SingleAsync(
            appointmentType =>
                appointmentType.Id == completedAppointment.AppointmentTypeId,
            cancellationToken);
        var schedule = await dbContext.DoctorSchedules.SingleAsync(
            schedule =>
                schedule.DoctorId == queue.DoctorId &&
                schedule.DayOfWeek == queue.Date.DayOfWeek,
            cancellationToken);
        var timeZone = scope.ServiceProvider.GetRequiredService<TimeZoneInfo>();
        var scheduleStartUtc = TimeZoneInfo.ConvertTimeToUtc(
            queue.Date.ToDateTime(schedule.StartTime),
            timeZone);
        var diagnostic =
            $"Queue date={queue.Date:O}; time zone={timeZone.Id}; " +
            $"schedule={schedule.StartTime:HH:mm}; duration={appointmentType.DurationMinutes}; " +
            $"schedule start UTC={scheduleStartUtc:O}; " +
            $"completed start={completedAppointment.ActualStartTime:O} " +
            $"({completedAppointment.ActualStartTime?.Kind}); " +
            $"completed end={completedAppointment.ActualEndTime:O} " +
            $"({completedAppointment.ActualEndTime?.Kind}); " +
            $"estimated start={item.EstimatedStartTime:O}; " +
            $"estimated delay={item.EstimatedDelayMinutes}.";
        Assert.True(item.EstimatedDelayMinutes == 1, diagnostic);
        Assert.Equal(9, item.EstimatedStartTime?.Hour);
        Assert.Equal(31, item.EstimatedStartTime?.Minute);
    }

    [Fact]
    public async Task GetQueueForDate_DoctorCannotUseIdRoute_ReturnsForbidden()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;
        const string patientEmail = "other-queue-patient@example.com";
        await RegisterPatientAsync(client, patientEmail);
        var queue = await SeedQueueAsync(factory, patientEmail);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                CreateDoctorToken(factory, queue.DoctorId));

        using var response = await client.GetAsync(
            $"/api/doctors/{queue.DoctorId}/queue?date={queue.Date:yyyy-MM-dd}",
            cancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<string> RegisterPatientAsync(
        HttpClient client,
        string email)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterPatientCommand(
                "Queue Patient",
                email,
                "Password123!",
                "01000000000",
                new DateOnly(1990, 1, 1)),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var token = await response.Content.ReadFromJsonAsync<JwtTokenResult>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(token);
        return token.Token;
    }

    private static async Task<SeededQueue> SeedQueueAsync(
        HospitalCareApiFactory factory,
        string patientEmail)
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var dbContext = services.GetRequiredService<ApplicationDbContext>();
        var patient = await dbContext.Patients.SingleAsync(
            candidate => candidate.Email == patientEmail,
            TestContext.Current.CancellationToken);
        var timeProvider = services.GetRequiredService<TimeProvider>();
        var timeZone = services.GetRequiredService<TimeZoneInfo>();
        var hospitalLocalNow = TimeZoneInfo.ConvertTime(
            timeProvider.GetUtcNow(),
            timeZone);
        var date = DateOnly.FromDateTime(hospitalLocalNow.DateTime).AddDays(3);
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
            EndTime = new TimeOnly(12, 0)
        });
        dbContext.AddRange(doctor, appointmentType);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var scheduleStart = TimeZoneInfo.ConvertTimeToUtc(
            date.ToDateTime(new TimeOnly(9, 0)),
            timeZone);
        dbContext.Appointments.AddRange(
            new Appointment
            {
                DoctorId = doctor.Id,
                PatientId = patient.Id,
                AppointmentTypeId = appointmentType.Id,
                AppointmentDate = date,
                QueuePosition = 1,
                Status = AppointmentStatus.Completed,
                ActualStartTime = scheduleStart.AddMinutes(10),
                ActualEndTime = scheduleStart.AddMinutes(31)
            },
            new Appointment
            {
                DoctorId = doctor.Id,
                PatientId = patient.Id,
                AppointmentTypeId = appointmentType.Id,
                AppointmentDate = date,
                QueuePosition = 2,
                Status = AppointmentStatus.Cancelled
            },
            new Appointment
            {
                DoctorId = doctor.Id,
                PatientId = patient.Id,
                AppointmentTypeId = appointmentType.Id,
                AppointmentDate = date,
                QueuePosition = 3,
                Status = AppointmentStatus.Queued
            });
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        return new SeededQueue(doctor.Id, date, 3);
    }

    private static string CreateDoctorToken(
        HospitalCareApiFactory factory,
        int doctorId)
    {
        using var scope = factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider
            .GetRequiredService<IJwtTokenService>();
        return tokenService.GenerateToken(
            $"doctor-user-{doctorId}",
            $"doctor-{doctorId}@example.com",
            [Roles.Doctor],
            doctorId: doctorId.ToString()).Token;
    }

    private sealed record SeededQueue(
        int DoctorId,
        DateOnly Date,
        int QueuedAppointmentId);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
