using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HospitalCare.API.Contracts.Departments;
using HospitalCare.API.Tests.Infrastructure;
using HospitalCare.Application.Common.Constants;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Common.Models;
using HospitalCare.Application.Features.Departments.DTOs;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalCare.API.Tests.Departments;

public sealed class DepartmentAdminControllerTests
{
    [Fact]
    public async Task Departments_AdminCreatesAndRenamesDepartment_PatientCanReadIt()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        AuthenticateAdmin(factory, client);

        using var createResponse = await client.PostAsJsonAsync(
            "/api/departments",
            new { Name = "Cardiology" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var created = await createResponse.Content
            .ReadFromJsonAsync<DepartmentDto>(
                TestContext.Current.CancellationToken);
        Assert.NotNull(created);
        Assert.True(created.IsActive);

        using var updateResponse = await client.PutAsJsonAsync(
            $"/api/departments/{created.Id}",
            new UpdateDepartmentRequest("Cardiology and Vascular"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content
            .ReadFromJsonAsync<DepartmentDto>(
                TestContext.Current.CancellationToken);

        Assert.Equal(
            new DepartmentDto(
                created.Id,
                "Cardiology and Vascular",
                true,
                false),
            updated);

        var patientToken = await RegisterPatientAsync(client);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", patientToken);
        using var listResponse = await client.GetAsync(
            "/api/departments",
            TestContext.Current.CancellationToken);
        var departments = await listResponse.Content
            .ReadFromJsonAsync<PagedResult<DepartmentDto>>(
                TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(departments);
        Assert.Contains(departments.Items, department =>
            department.Id == created.Id &&
            department.Name == "Cardiology and Vascular");
    }

    [Fact]
    public async Task CreateDepartment_PatientRequest_ReturnsForbidden()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        var token = await RegisterPatientAsync(client);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.PostAsJsonAsync(
            "/api/departments",
            new { Name = "Cardiology" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateDepartment_DuplicateName_ReturnsConflict()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        AuthenticateAdmin(factory, client);

        using var firstResponse = await client.PostAsJsonAsync(
            "/api/departments",
            new { Name = "Cardiology" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        using var duplicateResponse = await client.PostAsJsonAsync(
            "/api/departments",
            new { Name = " cardiology " },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateDepartment_UnknownId_ReturnsNotFound()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        AuthenticateAdmin(factory, client);

        using var response = await client.PutAsJsonAsync(
            "/api/departments/999",
            new UpdateDepartmentRequest("Cardiology"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
