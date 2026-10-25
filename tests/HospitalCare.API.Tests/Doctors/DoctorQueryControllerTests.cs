using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HospitalCare.API.Tests.Infrastructure;
using HospitalCare.Application.Common.Constants;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Common.Models;
using HospitalCare.Application.Features.Doctors.DTOs;
using HospitalCare.Domain.Entities;
using HospitalCare.Infrastructure.Persistence.DbInitializer;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalCare.API.Tests.Doctors;

public sealed class DoctorQueryControllerTests
{
    [Fact]
    public async Task GetDoctors_PatientRequestsInactiveDoctors_ReturnsActiveDoctorsOnly()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        var token = await RegisterPatientAsync(client);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        var seeded = await SeedDoctorsAsync(factory);

        using var filteredResponse = await client.GetAsync(
            $"/api/doctors?DepartmentId={seeded.DepartmentId}&IncludeInactive=true",
            TestContext.Current.CancellationToken);
        var filtered = await filteredResponse.Content
            .ReadFromJsonAsync<PagedResult<DoctorDto>>(
                TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, filteredResponse.StatusCode);
        Assert.NotNull(filtered);
        Assert.Equal(1, filtered.TotalCount);
        Assert.Equal(seeded.ActiveDoctorId, Assert.Single(filtered.Items).Id);

        using var inactiveResponse = await client.GetAsync(
            $"/api/doctors/{seeded.InactiveDoctorId}",
            TestContext.Current.CancellationToken);
        using var activeResponse = await client.GetAsync(
            $"/api/doctors/{seeded.ActiveDoctorId}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, inactiveResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, activeResponse.StatusCode);

        using var pagedResponse = await client.GetAsync(
            "/api/doctors?PageNumber=1&PageSize=1",
            TestContext.Current.CancellationToken);
        var page = await pagedResponse.Content
            .ReadFromJsonAsync<PagedResult<DoctorDto>>(
                TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, pagedResponse.StatusCode);
        Assert.NotNull(page);
        Assert.Equal(1, page.PageSize);
        Assert.Equal(2, page.TotalCount);
        Assert.Single(page.Items);
    }

    [Fact]
    public async Task GetDoctors_AdminIncludesInactiveDoctors_WhenRequested()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        AuthenticateAdmin(factory, client);
        var seeded = await SeedDoctorsAsync(factory);

        using var response = await client.GetAsync(
            "/api/doctors?IncludeInactive=true",
            TestContext.Current.CancellationToken);
        var doctors = await response.Content
            .ReadFromJsonAsync<PagedResult<DoctorDto>>(
                TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(doctors);
        Assert.Equal(3, doctors.TotalCount);
        Assert.Contains(doctors.Items, doctor =>
            doctor.Id == seeded.InactiveDoctorId && !doctor.IsActive);

        using var inactiveResponse = await client.GetAsync(
            $"/api/doctors/{seeded.InactiveDoctorId}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, inactiveResponse.StatusCode);
    }

    [Fact]
    public async Task GetDoctors_UnauthenticatedRequest_ReturnsUnauthorized()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/api/doctors",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetDoctors_InvalidDepartmentId_ReturnsBadRequest()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        var token = await RegisterPatientAsync(client);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.GetAsync(
            "/api/doctors?DepartmentId=0",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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

    private static async Task<SeededDoctors> SeedDoctorsAsync(
        HospitalCareApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        var firstDepartment = new Department { Name = "Cardiology" };
        var secondDepartment = new Department { Name = "Neurology" };
        dbContext.Departments.AddRange(firstDepartment, secondDepartment);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var activeDoctor = new Doctor
        {
            Name = "Dr. Active",
            DepartmentId = firstDepartment.Id
        };
        var inactiveDoctor = new Doctor
        {
            Name = "Dr. Inactive",
            DepartmentId = firstDepartment.Id,
            IsActive = false
        };
        var doctorInOtherDepartment = new Doctor
        {
            Name = "Dr. Other Department",
            DepartmentId = secondDepartment.Id
        };
        dbContext.Doctors.AddRange(
            activeDoctor,
            inactiveDoctor,
            doctorInOtherDepartment);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        return new SeededDoctors(
            firstDepartment.Id,
            activeDoctor.Id,
            inactiveDoctor.Id);
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

    private sealed record SeededDoctors(
        int DepartmentId,
        int ActiveDoctorId,
        int InactiveDoctorId);
}
