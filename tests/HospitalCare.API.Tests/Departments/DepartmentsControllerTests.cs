using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HospitalCare.API.Tests.Infrastructure;
using HospitalCare.Application.Common.Constants;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Common.Models;
using HospitalCare.Domain.Entities;
using HospitalCare.Infrastructure.Persistence.DbInitializer;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalCare.API.Tests.Departments;

public sealed class DepartmentsControllerTests
{
    [Fact]
    public async Task GetDepartments_PatientRequestsSecondPage_ReturnsOrderedPage()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        await SeedDepartmentsAsync(factory, 5);
        await AuthenticatePatientAsync(client);

        using var response = await client.GetAsync(
            "/api/departments?PageNumber=2&PageSize=2",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content
            .ReadFromJsonAsync<PagedResult<DepartmentResponse>>(
                TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(2, result.PageNumber);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal([3, 4], result.Items.Select(department => department.Id));
    }

    [Fact]
    public async Task GetDepartments_UnauthenticatedRequest_ReturnsUnauthorized()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/api/departments",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetDepartments_AdministratorRequestsDepartments_ReturnsForbidden()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider
            .GetRequiredService<IJwtTokenService>();
        var token = tokenService.GenerateToken(
            "admin-user",
            "admin@example.com",
            [Roles.Admin]).Token;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.GetAsync(
            "/api/departments",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetDepartments_InvalidPageSize_ReturnsBadRequest()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        await AuthenticatePatientAsync(client);

        using var response = await client.GetAsync(
            "/api/departments?PageSize=101",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
    }

    private static async Task AuthenticatePatientAsync(HttpClient client)
    {
        var registration = await client.PostAsJsonAsync(
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

        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        var token = await registration.Content.ReadFromJsonAsync<JwtTokenResult>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(token);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.Token);
    }

    private static async Task SeedDepartmentsAsync(
        HospitalCareApiFactory factory,
        int count)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        dbContext.Departments.AddRange(
            Enumerable.Range(1, count)
                .Select(index => new Department { Name = $"Department {index}" }));
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private sealed record DepartmentResponse(int Id, string Name, bool IsActive);
}
