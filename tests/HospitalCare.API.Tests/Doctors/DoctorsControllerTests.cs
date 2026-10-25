using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HospitalCare.API.Tests.Infrastructure;
using HospitalCare.Application.Common.Constants;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Common.Models;
using HospitalCare.Application.Features.Doctors.DTOs;
using HospitalCare.Domain.Entities;
using HospitalCare.Infrastructure.Identity;
using HospitalCare.Infrastructure.Persistence.DbInitializer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalCare.API.Tests.Doctors;

public sealed class DoctorsControllerTests
{
    [Fact]
    public async Task CreateDoctor_AdminCreatesAccount_DoctorCanLoginWithDoctorIdClaim()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        AuthenticateAdmin(factory, client);
        var departmentId = await SeedDepartmentAsync(factory);
        var email = $"doctor-{Guid.NewGuid():N}@example.com";

        using var createResponse = await client.PostAsJsonAsync(
            "/api/doctors",
            new
            {
                Name = "Dr. Example",
                Email = email,
                Password = "Password123!",
                DepartmentId = departmentId
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var doctor = await createResponse.Content
            .ReadFromJsonAsync<DoctorDto>(
                TestContext.Current.CancellationToken);
        Assert.NotNull(doctor);
        Assert.Equal(departmentId, doctor.DepartmentId);
        Assert.True(doctor.IsActive);

        using var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { Email = email, Password = "Password123!" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var token = await loginResponse.Content
            .ReadFromJsonAsync<JwtTokenResult>(
                TestContext.Current.CancellationToken);
        Assert.NotNull(token);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token.Token);
        Assert.Contains(jwt.Claims, claim =>
            claim.Type == "doctorId" && claim.Value == doctor.Id.ToString());

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.Token);
        using var patientProfileResponse = await client.GetAsync(
            "/api/patients/me",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, patientProfileResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        var persistedDoctor = await dbContext.Doctors
            .AsNoTracking()
            .SingleAsync(
                candidate => candidate.Id == doctor.Id,
                TestContext.Current.CancellationToken);

        Assert.NotNull(user);
        Assert.Equal(user.Id, persistedDoctor.UserId);
        Assert.Contains(Roles.Doctor, await userManager.GetRolesAsync(user));
    }

    [Fact]
    public async Task CreateDoctor_InvalidPassword_DoesNotPersistAccountOrDoctor()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        AuthenticateAdmin(factory, client);
        var departmentId = await SeedDepartmentAsync(factory);
        var email = $"doctor-{Guid.NewGuid():N}@example.com";

        using var response = await client.PostAsJsonAsync(
            "/api/doctors",
            new
            {
                Name = "Dr. Invalid",
                Email = email,
                Password = "weak",
                DepartmentId = departmentId
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        Assert.Null(await userManager.FindByEmailAsync(email));
        Assert.False(await dbContext.Doctors.AnyAsync(
            doctor => doctor.Name == "Dr. Invalid",
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateDoctor_PatientRequest_ReturnsForbidden()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        var token = await RegisterPatientAsync(client);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.PostAsJsonAsync(
            "/api/doctors",
            new
            {
                Name = "Dr. Example",
                Email = "doctor@example.com",
                Password = "Password123!",
                DepartmentId = 1
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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

    private static async Task<int> SeedDepartmentAsync(
        HospitalCareApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        var department = new Department { Name = "Cardiology" };
        dbContext.Departments.Add(department);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return department.Id;
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
