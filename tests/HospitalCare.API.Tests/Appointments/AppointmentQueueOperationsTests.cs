using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HospitalCare.API.Tests.Infrastructure;
using HospitalCare.Application.Common.Constants;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Features.Appointments.Commands;
using HospitalCare.Application.Features.Appointments.DTOs;
using HospitalCare.Application.Features.Departments.DTOs;
using HospitalCare.Application.Features.Doctors.DTOs;
using HospitalCare.Domain.Entities;
using HospitalCare.Domain.Enums;
using HospitalCare.Infrastructure.Persistence.DbInitializer;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalCare.API.Tests.Appointments;

public sealed class AppointmentQueueOperationsTests
{
    [Fact]
    public async Task CheckIn_CallStartAndComplete_TracksAppointmentProgress()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        var queue = await SeedQueueAsync(factory, 1);
        Authenticate(factory, client, Roles.Receptionist);

        using var checkInResponse = await client.PostAsync(
            $"/api/appointments/{queue.AppointmentIds[0]}/check-in",
            content: null,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, checkInResponse.StatusCode);
        Authenticate(factory, client, Roles.Doctor, queue.DoctorId);

        using var callResponse = await client.PostAsync(
            $"/api/doctors/me/queue/call-next?date={queue.Date:yyyy-MM-dd}",
            content: null,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, callResponse.StatusCode);
        var calledAppointment = await callResponse.Content
            .ReadFromJsonAsync<AppointmentDto>(
                TestContext.Current.CancellationToken);
        Assert.NotNull(calledAppointment);
        Assert.Equal(queue.AppointmentIds[0], calledAppointment.Id);

        using var startResponse = await client.PostAsync(
            $"/api/appointments/{calledAppointment.Id}/start",
            content: null,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);

        using var completeResponse = await client.PostAsJsonAsync(
            $"/api/appointments/{calledAppointment.Id}/complete",
            new { Notes = "Consultation completed." },
            TestContext.Current.CancellationToken);
        var completedAppointment = await completeResponse.Content
            .ReadFromJsonAsync<AppointmentDto>(
                TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);
        Assert.NotNull(completedAppointment);
        Assert.Equal(AppointmentStatus.Completed, completedAppointment.Status);
        Assert.NotNull(completedAppointment.ActualStartTime);
        Assert.NotNull(completedAppointment.ActualEndTime);
        Assert.Equal("Consultation completed.", completedAppointment.Notes);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        var persistedAppointment = await dbContext.Appointments
            .AsNoTracking()
            .SingleAsync(
                appointment => appointment.Id == calledAppointment.Id,
                TestContext.Current.CancellationToken);
        Assert.NotNull(persistedAppointment.CheckedInAt);
        Assert.NotNull(persistedAppointment.CalledAt);
    }

    [Fact]
    public async Task CallNext_ReceptionistCallsFirstCheckedInPatient()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        var queue = await SeedQueueAsync(factory, 2);
        await CheckInAsync(factory, queue.AppointmentIds[1]);
        Authenticate(factory, client, Roles.Receptionist);

        using var response = await client.PostAsync(
            $"/api/doctors/{queue.DoctorId}/queue/call-next?date={queue.Date:yyyy-MM-dd}",
            content: null,
            TestContext.Current.CancellationToken);
        var appointment = await response.Content.ReadFromJsonAsync<AppointmentDto>(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(appointment);
        Assert.Equal(queue.AppointmentIds[1], appointment.Id);
    }

    [Fact]
    public async Task StartAppointment_DoctorRequestsAnotherDoctorsAppointment_ReturnsForbidden()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        var queue = await SeedQueueAsync(factory, 1);
        Authenticate(factory, client, Roles.Doctor, queue.DoctorId + 1);

        using var response = await client.PostAsync(
            $"/api/appointments/{queue.AppointmentIds[0]}/start",
            content: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ProcessNoShowSwap_FirstMissWithoutNextPatient_LeavesAppointmentQueued()
    {
        using var factory = new HospitalCareApiFactory();
        var queue = await SeedQueueAsync(
            factory,
            1,
            calledMinutesAgo: Appointment.NoShowGracePeriodMinutes + 1);

        var processed = await ProcessNoShowAsync(
            factory,
            queue.AppointmentIds[0]);
        var appointment = await ReadAppointmentAsync(
            factory,
            queue.AppointmentIds[0]);

        Assert.False(processed);
        Assert.Equal(AppointmentStatus.Queued, appointment.Status);
        Assert.Equal(0, appointment.SkipCount);
        Assert.NotNull(appointment.CalledAt);
    }

    [Fact]
    public async Task CheckIn_AfterCallDeadlineWithoutNextPatient_AcceptsArrival()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        var queue = await SeedQueueAsync(
            factory,
            1,
            calledMinutesAgo: Appointment.NoShowGracePeriodMinutes + 1);
        Authenticate(factory, client, Roles.Receptionist);

        using var response = await client.PostAsync(
            $"/api/appointments/{queue.AppointmentIds[0]}/check-in",
            content: null,
            TestContext.Current.CancellationToken);
        var appointment = await ReadAppointmentAsync(
            factory,
            queue.AppointmentIds[0]);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(appointment.CheckedInAt);
        Assert.Null(appointment.CalledAt);
        Assert.Equal(0, appointment.SkipCount);

        Authenticate(factory, client, Roles.Doctor, queue.DoctorId);
        using var callResponse = await client.PostAsync(
            $"/api/doctors/me/queue/call-next?date={queue.Date:yyyy-MM-dd}",
            content: null,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, callResponse.StatusCode);
    }

    [Fact]
    public async Task CheckIn_AfterCallDeadlineWithNextPatient_WaitsForNoShowSwap()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        var queue = await SeedQueueAsync(
            factory,
            2,
            calledMinutesAgo: Appointment.NoShowGracePeriodMinutes + 1);
        Authenticate(factory, client, Roles.Receptionist);

        using var lateResponse = await client.PostAsync(
            $"/api/appointments/{queue.AppointmentIds[0]}/check-in",
            content: null,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, lateResponse.StatusCode);
        var processed = await ProcessNoShowAsync(
            factory,
            queue.AppointmentIds[0]);
        using var checkInResponse = await client.PostAsync(
            $"/api/appointments/{queue.AppointmentIds[0]}/check-in",
            content: null,
            TestContext.Current.CancellationToken);
        var appointment = await ReadAppointmentAsync(
            factory,
            queue.AppointmentIds[0]);

        Assert.True(processed);
        Assert.Equal(HttpStatusCode.OK, checkInResponse.StatusCode);
        Assert.Equal(1, appointment.SkipCount);
        Assert.NotNull(appointment.CheckedInAt);
    }

    [Fact]
    public async Task ProcessNoShowSwap_FirstMissWithNextPatient_SwapsQueuedPositions()
    {
        using var factory = new HospitalCareApiFactory();
        var queue = await SeedQueueAsync(
            factory,
            2,
            calledMinutesAgo: Appointment.NoShowGracePeriodMinutes + 1,
            canceledPosition: 2);

        var processed = await ProcessNoShowAsync(
            factory,
            queue.AppointmentIds[0]);
        var missedAppointment = await ReadAppointmentAsync(
            factory,
            queue.AppointmentIds[0]);
        var nextAppointment = await ReadAppointmentAsync(
            factory,
            queue.AppointmentIds[1]);

        Assert.True(processed);
        Assert.Equal(queue.AppointmentIds[1], nextAppointment.Id);
        Assert.Equal(1, nextAppointment.QueuePosition);
        Assert.Equal(3, missedAppointment.QueuePosition);
        Assert.Equal(1, missedAppointment.SkipCount);
        Assert.Null(missedAppointment.CalledAt);
    }

    [Fact]
    public async Task ProcessNoShowSwap_SecondMiss_PenalizesWithoutRefundingFee()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        var queue = await SeedQueueAsync(
            factory,
            1,
            calledMinutesAgo: Appointment.NoShowGracePeriodMinutes + 1,
            skipCount: 1,
            includePayment: true);
        Authenticate(factory, client, Roles.Admin);
        using var deactivationResponse = await client.PostAsync(
            $"/api/doctors/{queue.DoctorId}/deactivate",
            content: null,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, deactivationResponse.StatusCode);

        var processed = await ProcessNoShowAsync(
            factory,
            queue.AppointmentIds[0]);
        var appointment = await ReadAppointmentAsync(
            factory,
            queue.AppointmentIds[0]);
        var paymentStatus = await ReadPaymentStatusAsync(
            factory,
            queue.PaymentTransactionId!.Value);
        var doctor = await ReadDoctorAsync(factory, queue.DoctorId);

        Assert.True(processed);
        Assert.Equal(AppointmentStatus.NoShowPenalized, appointment.Status);
        Assert.Equal(PaymentStatus.Succeeded, paymentStatus);
        Assert.False(doctor.IsActive);
        Assert.False(doctor.PendingDeactivation);
    }

    [Fact]
    public async Task DeactivateDepartment_WithOpenQueue_WaitsUntilQueueDrains()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        var queue = await SeedQueueAsync(factory, 1);
        var secondDoctorQueue = await AddDoctorQueueAsync(
            factory,
            queue.DepartmentId);
        await SetAttendanceAsync(factory, queue.AppointmentIds[0]);
        await SetAttendanceAsync(factory, secondDoctorQueue.AppointmentId);
        Authenticate(factory, client, Roles.Admin);

        using var deactivateResponse = await client.PostAsync(
            $"/api/departments/{queue.DepartmentId}/deactivate",
            content: null,
            TestContext.Current.CancellationToken);
        var department = await deactivateResponse.Content
            .ReadFromJsonAsync<DepartmentDto>(
                TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);
        Assert.NotNull(department);
        Assert.True(department.IsActive);
        Assert.True(department.PendingDeactivation);
        var pendingDoctor = await ReadDoctorAsync(factory, queue.DoctorId);
        Assert.True(pendingDoctor.IsActive);
        Assert.True(pendingDoctor.PendingDeactivation);
        var secondPendingDoctor = await ReadDoctorAsync(
            factory,
            secondDoctorQueue.DoctorId);
        Assert.True(secondPendingDoctor.IsActive);
        Assert.True(secondPendingDoctor.PendingDeactivation);

        Authenticate(factory, client, Roles.Doctor, queue.DoctorId);
        using var startResponse = await client.PostAsync(
            $"/api/appointments/{queue.AppointmentIds[0]}/start",
            content: null,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);
        using var completeResponse = await client.PostAsJsonAsync(
            $"/api/appointments/{queue.AppointmentIds[0]}/complete",
            new { Notes = (string?)null },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);

        var pendingDepartment = await ReadDepartmentAsync(
            factory,
            queue.DepartmentId);
        Assert.True(pendingDepartment.IsActive);
        Assert.True(pendingDepartment.PendingDeactivation);

        Authenticate(factory, client, Roles.Doctor, secondDoctorQueue.DoctorId);
        using var secondStartResponse = await client.PostAsync(
            $"/api/appointments/{secondDoctorQueue.AppointmentId}/start",
            content: null,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, secondStartResponse.StatusCode);
        using var secondCompleteResponse = await client.PostAsJsonAsync(
            $"/api/appointments/{secondDoctorQueue.AppointmentId}/complete",
            new { Notes = (string?)null },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, secondCompleteResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        var inactiveDepartment = await dbContext.Departments
            .AsNoTracking()
            .SingleAsync(
                candidate => candidate.Id == queue.DepartmentId,
                TestContext.Current.CancellationToken);
        var inactiveDoctor = await dbContext.Doctors
            .AsNoTracking()
            .SingleAsync(
                candidate => candidate.Id == queue.DoctorId,
                TestContext.Current.CancellationToken);
        Assert.False(inactiveDepartment.IsActive);
        Assert.False(inactiveDepartment.PendingDeactivation);
        Assert.False(inactiveDoctor.IsActive);
        Assert.False(inactiveDoctor.PendingDeactivation);
    }

    [Fact]
    public async Task DeactivateDoctor_WithoutOpenQueue_DeactivatesImmediately()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        var queue = await SeedQueueAsync(factory, 0);
        Authenticate(factory, client, Roles.Admin);

        using var response = await client.PostAsync(
            $"/api/doctors/{queue.DoctorId}/deactivate",
            content: null,
            TestContext.Current.CancellationToken);
        var doctor = await response.Content.ReadFromJsonAsync<DoctorDto>(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(doctor);
        Assert.False(doctor.IsActive);
        Assert.False(doctor.PendingDeactivation);
    }

    [Fact]
    public async Task CancelAppointment_PendingDoctorQueueDrains_DeactivatesDoctor()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        var queue = await SeedQueueAsync(
            factory,
            1,
            includePayment: true);
        Authenticate(factory, client, Roles.Admin);

        using var deactivateResponse = await client.PostAsync(
            $"/api/doctors/{queue.DoctorId}/deactivate",
            content: null,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);

        using var cancelResponse = await client.PostAsync(
            $"/api/appointments/{queue.AppointmentIds[0]}/cancel",
            content: null,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);

        var doctor = await ReadDoctorAsync(factory, queue.DoctorId);
        var paymentStatus = await ReadPaymentStatusAsync(
            factory,
            queue.PaymentTransactionId!.Value);
        Assert.False(doctor.IsActive);
        Assert.False(doctor.PendingDeactivation);
        Assert.Equal(PaymentStatus.Refunded, paymentStatus);
    }

    private static async Task<SeededQueue> SeedQueueAsync(
        HospitalCareApiFactory factory,
        int queuedAppointmentCount,
        int? calledMinutesAgo = null,
        int skipCount = 0,
        int? canceledPosition = null,
        bool includePayment = false)
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var dbContext = services.GetRequiredService<ApplicationDbContext>();
        var timeProvider = services.GetRequiredService<TimeProvider>();
        var timeZone = services.GetRequiredService<TimeZoneInfo>();
        var now = timeProvider.GetUtcNow();
        var localNow = TimeZoneInfo.ConvertTime(now, timeZone);
        var date = DateOnly.FromDateTime(localNow.DateTime);
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
        var patients = Enumerable.Range(0, queuedAppointmentCount)
            .Select(index => new Patient
            {
                UserId = $"queue-patient-{Guid.NewGuid():N}",
                Name = $"Queue Patient {index}",
                Email = $"queue-patient-{Guid.NewGuid():N}@example.com",
                Phone = "01000000000",
                DateOfBirth = new DateOnly(1990, 1, 1)
            })
            .ToArray();
        dbContext.AddRange(doctor, appointmentType);
        dbContext.Patients.AddRange(patients);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var appointments = new List<Appointment>();
        for (var index = 0; index < queuedAppointmentCount; index++)
        {
            var queuePosition = index + 1;
            if (canceledPosition.HasValue &&
                queuePosition >= canceledPosition.Value)
            {
                queuePosition++;
            }

            appointments.Add(new Appointment
            {
                DoctorId = doctor.Id,
                PatientId = patients[index].Id,
                AppointmentTypeId = appointmentType.Id,
                AppointmentDate = date,
                QueuePosition = queuePosition,
                SkipCount = index == 0 ? skipCount : 0,
                CalledAt = index == 0 && calledMinutesAgo.HasValue
                    ? now.UtcDateTime.AddMinutes(-calledMinutesAgo.Value)
                    : null
            });
        }

        if (canceledPosition.HasValue)
        {
            appointments.Add(new Appointment
            {
                DoctorId = doctor.Id,
                PatientId = patients[0].Id,
                AppointmentTypeId = appointmentType.Id,
                AppointmentDate = date,
                QueuePosition = canceledPosition.Value,
                Status = AppointmentStatus.Cancelled
            });
        }

        dbContext.Appointments.AddRange(appointments);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        int? paymentTransactionId = null;
        if (includePayment)
        {
            var payment = new PaymentTransaction
            {
                PatientId = patients[0].Id,
                DoctorId = doctor.Id,
                AppointmentTypeId = appointmentType.Id,
                RequestedDate = date,
                Amount = appointmentType.BookingFee,
                Status = PaymentStatus.Succeeded,
                GatewayReference = $"payment-{Guid.NewGuid():N}",
                CreatedAt = now.UtcDateTime
            };
            dbContext.PaymentTransactions.Add(payment);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
            appointments[0].PaymentTransactionId = payment.Id;
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
            paymentTransactionId = payment.Id;
        }

        return new SeededQueue(
            department.Id,
            doctor.Id,
            date,
            appointments
                .Where(appointment =>
                    appointment.Status == AppointmentStatus.Queued)
                .OrderBy(appointment => appointment.QueuePosition)
                .Select(appointment => appointment.Id)
                .ToArray(),
            paymentTransactionId);
    }

    private static async Task CheckInAsync(
        HospitalCareApiFactory factory,
        int appointmentId)
    {
        using var client = factory.CreateClient();
        Authenticate(factory, client, Roles.Receptionist);
        using var response = await client.PostAsync(
            $"/api/appointments/{appointmentId}/check-in",
            content: null,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<bool> ProcessNoShowAsync(
        HospitalCareApiFactory factory,
        int appointmentId)
    {
        using var scope = factory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(
            new ProcessNoShowSwapCommand(appointmentId),
            TestContext.Current.CancellationToken);
    }

    private static async Task<Appointment> ReadAppointmentAsync(
        HospitalCareApiFactory factory,
        int appointmentId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        return await dbContext.Appointments
            .AsNoTracking()
            .SingleAsync(
                appointment => appointment.Id == appointmentId,
                TestContext.Current.CancellationToken);
    }

    private static async Task<PaymentStatus> ReadPaymentStatusAsync(
        HospitalCareApiFactory factory,
        int paymentTransactionId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        var payment = await dbContext.PaymentTransactions
            .AsNoTracking()
            .SingleAsync(
                transaction => transaction.Id == paymentTransactionId,
                TestContext.Current.CancellationToken);
        return payment.Status;
    }

    private static async Task<Doctor> ReadDoctorAsync(
        HospitalCareApiFactory factory,
        int doctorId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        return await dbContext.Doctors
            .AsNoTracking()
            .SingleAsync(
                doctor => doctor.Id == doctorId,
                TestContext.Current.CancellationToken);
    }

    private static async Task SetAttendanceAsync(
        HospitalCareApiFactory factory,
        int appointmentId)
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var dbContext = services.GetRequiredService<ApplicationDbContext>();
        var now = services.GetRequiredService<TimeProvider>()
            .GetUtcNow()
            .UtcDateTime;
        var appointment = await dbContext.Appointments.SingleAsync(
            candidate => candidate.Id == appointmentId,
            TestContext.Current.CancellationToken);
        appointment.CheckedInAt = now.AddMinutes(-1);
        appointment.CalledAt = now.AddMinutes(-1);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<DoctorQueue> AddDoctorQueueAsync(
        HospitalCareApiFactory factory,
        int departmentId)
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var dbContext = services.GetRequiredService<ApplicationDbContext>();
        var timeProvider = services.GetRequiredService<TimeProvider>();
        var timeZone = services.GetRequiredService<TimeZoneInfo>();
        var localNow = TimeZoneInfo.ConvertTime(
            timeProvider.GetUtcNow(),
            timeZone);
        var date = DateOnly.FromDateTime(localNow.DateTime);
        var doctor = new Doctor
        {
            Name = $"Doctor-{Guid.NewGuid():N}",
            DepartmentId = departmentId
        };
        var patient = new Patient
        {
            UserId = $"queue-patient-{Guid.NewGuid():N}",
            Name = "Department Queue Patient",
            Email = $"queue-patient-{Guid.NewGuid():N}@example.com",
            Phone = "01000000000",
            DateOfBirth = new DateOnly(1990, 1, 1)
        };
        var appointmentType = new AppointmentType
        {
            Name = $"Consultation-{Guid.NewGuid():N}",
            DurationMinutes = 30,
            BookingFee = 100
        };
        dbContext.AddRange(doctor, patient, appointmentType);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var appointment = new Appointment
        {
            DoctorId = doctor.Id,
            PatientId = patient.Id,
            AppointmentTypeId = appointmentType.Id,
            AppointmentDate = date,
            QueuePosition = 1
        };
        dbContext.Appointments.Add(appointment);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        return new DoctorQueue(doctor.Id, appointment.Id);
    }

    private static async Task<Department> ReadDepartmentAsync(
        HospitalCareApiFactory factory,
        int departmentId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        return await dbContext.Departments
            .AsNoTracking()
            .SingleAsync(
                department => department.Id == departmentId,
                TestContext.Current.CancellationToken);
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
        var token = tokenService.GenerateToken(
            $"{role.ToLowerInvariant()}-user",
            $"{role.ToLowerInvariant()}@example.com",
            [role],
            doctorId: doctorId?.ToString()).Token;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    private sealed record SeededQueue(
        int DepartmentId,
        int DoctorId,
        DateOnly Date,
        IReadOnlyList<int> AppointmentIds,
        int? PaymentTransactionId);

    private sealed record DoctorQueue(int DoctorId, int AppointmentId);
}
