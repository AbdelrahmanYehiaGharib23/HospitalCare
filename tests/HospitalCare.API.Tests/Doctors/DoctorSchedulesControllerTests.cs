using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HospitalCare.API.Tests.Infrastructure;
using HospitalCare.Application.Common.Constants;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Common.Models;
using HospitalCare.Application.Features.DoctorSchedules.DTOs;
using HospitalCare.Domain.Entities;
using HospitalCare.Infrastructure.Persistence.DbInitializer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalCare.API.Tests.Doctors;

public sealed class DoctorSchedulesControllerTests
{
    [Fact]
    public async Task UpdateMySchedule_DoctorReplacesSchedule_ReturnsNewRanges()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        var doctorId = await SeedDoctorAsync(factory);
        var oldScheduleId = await SeedScheduleAsync(
            factory,
            doctorId,
            DayOfWeek.Friday,
            new TimeOnly(9, 0),
            new TimeOnly(12, 0));
        AuthenticateDoctor(factory, client, doctorId);

        using var response = await client.PutAsJsonAsync(
            "/api/doctors/me/schedule",
            new
            {
                Ranges = new[]
                {
                    new
                    {
                        DayOfWeek = DayOfWeek.Monday,
                        StartTime = new TimeOnly(9, 0),
                        EndTime = new TimeOnly(12, 0)
                    },
                    new
                    {
                        DayOfWeek = DayOfWeek.Monday,
                        StartTime = new TimeOnly(13, 0),
                        EndTime = new TimeOnly(16, 0)
                    }
                }
            },
            TestContext.Current.CancellationToken);
        var schedules = await response.Content
            .ReadFromJsonAsync<DoctorScheduleDto[]>(
                TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(schedules);
        Assert.Equal(2, schedules.Length);
        Assert.All(schedules, schedule => Assert.Equal(doctorId, schedule.DoctorId));
        Assert.All(schedules, schedule =>
            Assert.Equal(DayOfWeek.Monday, schedule.DayOfWeek));

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        var persistedSchedules = await dbContext.DoctorSchedules
            .AsNoTracking()
            .Where(schedule => schedule.DoctorId == doctorId)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, persistedSchedules.Length);
        Assert.DoesNotContain(
            persistedSchedules,
            schedule => schedule.Id == oldScheduleId);
    }

    [Fact]
    public async Task UpdateMySchedule_EmptyRanges_ClearsSchedule()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        var doctorId = await SeedDoctorAsync(factory);
        await SeedScheduleAsync(
            factory,
            doctorId,
            DayOfWeek.Friday,
            new TimeOnly(9, 0),
            new TimeOnly(12, 0));
        AuthenticateDoctor(factory, client, doctorId);

        using var response = await client.PutAsJsonAsync(
            "/api/doctors/me/schedule",
            new { Ranges = Array.Empty<object>() },
            TestContext.Current.CancellationToken);
        var schedules = await response.Content
            .ReadFromJsonAsync<DoctorScheduleDto[]>(
                TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(schedules);
        Assert.Empty(schedules);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        Assert.False(await dbContext.DoctorSchedules.AnyAsync(
            schedule => schedule.DoctorId == doctorId,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateMySchedule_OverlappingRanges_ReturnsBadRequestAndKeepsSchedule()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        var doctorId = await SeedDoctorAsync(factory);
        var existingScheduleId = await SeedScheduleAsync(
            factory,
            doctorId,
            DayOfWeek.Friday,
            new TimeOnly(9, 0),
            new TimeOnly(12, 0));
        AuthenticateDoctor(factory, client, doctorId);

        using var response = await client.PutAsJsonAsync(
            "/api/doctors/me/schedule",
            new
            {
                Ranges = new[]
                {
                    new
                    {
                        DayOfWeek = DayOfWeek.Monday,
                        StartTime = new TimeOnly(9, 0),
                        EndTime = new TimeOnly(12, 0)
                    },
                    new
                    {
                        DayOfWeek = DayOfWeek.Monday,
                        StartTime = new TimeOnly(11, 0),
                        EndTime = new TimeOnly(14, 0)
                    }
                }
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        var persistedSchedules = await dbContext.DoctorSchedules
            .AsNoTracking()
            .Where(schedule => schedule.DoctorId == doctorId)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        Assert.Equal(existingScheduleId, Assert.Single(persistedSchedules).Id);
    }

    [Fact]
    public async Task UpdateMySchedule_PatientRequest_ReturnsForbidden()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        var token = await RegisterPatientAsync(client);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.PutAsJsonAsync(
            "/api/doctors/me/schedule",
            new { Ranges = Array.Empty<object>() },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetDoctorSchedule_AdminCanReadInactiveDoctorSchedule()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        var doctorId = await SeedDoctorAsync(factory, isActive: false);
        await SeedScheduleAsync(
            factory,
            doctorId,
            DayOfWeek.Tuesday,
            new TimeOnly(10, 0),
            new TimeOnly(14, 0));
        AuthenticateAdmin(factory, client);

        using var response = await client.GetAsync(
            $"/api/doctors/{doctorId}/schedule",
            TestContext.Current.CancellationToken);
        var schedules = await response.Content
            .ReadFromJsonAsync<DoctorScheduleDto[]>(
                TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(schedules);
        Assert.Equal(doctorId, Assert.Single(schedules).DoctorId);
    }

    [Fact]
    public async Task GetMyDoctorSchedule_UsesClaimAndRejectsDoctorIdRoute()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        var ownDoctorId = await SeedDoctorAsync(factory);
        var otherDoctorId = await SeedDoctorAsync(factory);
        await SeedScheduleAsync(
            factory,
            ownDoctorId,
            DayOfWeek.Wednesday,
            new TimeOnly(9, 0),
            new TimeOnly(12, 0));
        AuthenticateDoctor(factory, client, ownDoctorId);

        using var ownResponse = await client.GetAsync(
            "/api/doctors/me/schedule",
            TestContext.Current.CancellationToken);
        using var otherResponse = await client.GetAsync(
            $"/api/doctors/{otherDoctorId}/schedule",
            TestContext.Current.CancellationToken);
        using var ownIdResponse = await client.GetAsync(
            $"/api/doctors/{ownDoctorId}/schedule",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, ownResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, otherResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, ownIdResponse.StatusCode);
    }

    [Fact]
    public async Task GetDoctorSchedule_PatientCanReadActiveDoctorScheduleOnly()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        var activeDoctorId = await SeedDoctorAsync(factory);
        var inactiveDoctorId = await SeedDoctorAsync(factory, isActive: false);
        await SeedScheduleAsync(
            factory,
            activeDoctorId,
            DayOfWeek.Thursday,
            new TimeOnly(9, 0),
            new TimeOnly(12, 0));
        await SeedScheduleAsync(
            factory,
            inactiveDoctorId,
            DayOfWeek.Thursday,
            new TimeOnly(13, 0),
            new TimeOnly(16, 0));
        var token = await RegisterPatientAsync(client);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        using var activeResponse = await client.GetAsync(
            $"/api/doctors/{activeDoctorId}/schedule",
            TestContext.Current.CancellationToken);
        using var inactiveResponse = await client.GetAsync(
            $"/api/doctors/{inactiveDoctorId}/schedule",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, activeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, inactiveResponse.StatusCode);
    }

    private static async Task<int> SeedDoctorAsync(
        HospitalCareApiFactory factory,
        bool isActive = true)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        var department = new Department { Name = $"Department-{Guid.NewGuid():N}" };
        dbContext.Departments.Add(department);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var doctor = new Doctor
        {
            Name = $"Doctor-{Guid.NewGuid():N}",
            DepartmentId = department.Id,
            IsActive = isActive
        };
        dbContext.Doctors.Add(doctor);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return doctor.Id;
    }

    private static async Task<int> SeedScheduleAsync(
        HospitalCareApiFactory factory,
        int doctorId,
        DayOfWeek dayOfWeek,
        TimeOnly startTime,
        TimeOnly endTime)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        var schedule = new DoctorSchedule
        {
            DoctorId = doctorId,
            DayOfWeek = dayOfWeek,
            StartTime = startTime,
            EndTime = endTime
        };
        dbContext.DoctorSchedules.Add(schedule);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return schedule.Id;
    }

    private static void AuthenticateAdmin(
        HospitalCareApiFactory factory,
        HttpClient client)
    {
        using var scope = factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider
            .GetRequiredService<IJwtTokenService>();
        var token = tokenService.GenerateToken(
            "admin-user",
            "admin@example.com",
            [Roles.Admin]).Token;

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    private static void AuthenticateDoctor(
        HospitalCareApiFactory factory,
        HttpClient client,
        int doctorId)
    {
        using var scope = factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider
            .GetRequiredService<IJwtTokenService>();
        var token = tokenService.GenerateToken(
            "doctor-user",
            "doctor@example.com",
            [Roles.Doctor],
            doctorId: doctorId.ToString()).Token;

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    private static async Task<string> RegisterPatientAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
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
}
