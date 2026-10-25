using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HospitalCare.API.Tests.Infrastructure;
using HospitalCare.Application.Common.Constants;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Common.Models;
using HospitalCare.Application.Features.AppointmentTypes.DTOs;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalCare.API.Tests.AppointmentTypes;

public sealed class AppointmentTypesControllerTests
{
    [Fact]
    public async Task AppointmentTypes_AdminCreatesUpdatesAndListsTypes_ReturnsSeededAndUpdatedValues()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        AuthenticateAdmin(factory, client);

        using var initialResponse = await client.GetAsync(
            "/api/appointment-types",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, initialResponse.StatusCode);
        var initialResult = await initialResponse.Content
            .ReadFromJsonAsync<PagedResult<AppointmentTypeDto>>(
                TestContext.Current.CancellationToken);

        Assert.NotNull(initialResult);
        Assert.Equal(2, initialResult.TotalCount);
        Assert.Contains(initialResult.Items, appointmentType =>
            appointmentType.Name == "Consultation" &&
            appointmentType.DurationMinutes == 30 &&
            appointmentType.BookingFee == 100m);
        Assert.Contains(initialResult.Items, appointmentType =>
            appointmentType.Name == "Follow-up" &&
            appointmentType.DurationMinutes == 15 &&
            appointmentType.BookingFee == 50m);

        using var createResponse = await client.PostAsJsonAsync(
            "/api/appointment-types",
            new
            {
                Name = "Extended Consultation",
                DurationMinutes = 45,
                BookingFee = 125.50m
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var created = await createResponse.Content
            .ReadFromJsonAsync<AppointmentTypeDto>(
                TestContext.Current.CancellationToken);
        Assert.NotNull(created);

        using var updateResponse = await client.PutAsJsonAsync(
            $"/api/appointment-types/{created.Id}",
            new
            {
                Name = "Specialist Consultation",
                DurationMinutes = 60,
                BookingFee = 150m
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content
            .ReadFromJsonAsync<AppointmentTypeDto>(
                TestContext.Current.CancellationToken);

        Assert.NotNull(updated);
        Assert.Equal(
            new AppointmentTypeDto(
                created.Id,
                "Specialist Consultation",
                60,
                150m),
            updated);

        using var listResponse = await client.GetAsync(
            "/api/appointment-types",
            TestContext.Current.CancellationToken);
        var listed = await listResponse.Content
            .ReadFromJsonAsync<PagedResult<AppointmentTypeDto>>(
                TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(listed);
        Assert.Equal(3, listed.TotalCount);
        Assert.Contains(listed.Items, appointmentType =>
            appointmentType.Id == created.Id &&
            appointmentType.Name == "Specialist Consultation" &&
            appointmentType.DurationMinutes == 60 &&
            appointmentType.BookingFee == 150m);
    }

    [Fact]
    public async Task GetAppointmentTypes_UnauthenticatedRequest_ReturnsUnauthorized()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/api/appointment-types",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAppointmentTypes_PatientRequest_ReturnsAvailableTypes()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        var token = await RegisterPatientAsync(client);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.GetAsync(
            "/api/appointment-types",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content
            .ReadFromJsonAsync<PagedResult<AppointmentTypeDto>>(
                TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(2, result.TotalCount);
        Assert.Contains(result.Items, item =>
            item.Name == "Consultation" &&
            item.DurationMinutes == 30 &&
            item.BookingFee == 100m);
        Assert.Contains(result.Items, item =>
            item.Name == "Follow-up" &&
            item.DurationMinutes == 15 &&
            item.BookingFee == 50m);
    }

    [Fact]
    public async Task CreateAppointmentType_InvalidFee_ReturnsBadRequest()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        AuthenticateAdmin(factory, client);

        using var response = await client.PostAsJsonAsync(
            "/api/appointment-types",
            new
            {
                Name = "Invalid Appointment Type",
                DurationMinutes = 30,
                BookingFee = 0m
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task CreateAppointmentType_DuplicateName_ReturnsConflict()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        AuthenticateAdmin(factory, client);

        using var response = await client.PostAsJsonAsync(
            "/api/appointment-types",
            new
            {
                Name = " consultation ",
                DurationMinutes = 30,
                BookingFee = 100m
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UpdateAppointmentType_UnknownId_ReturnsNotFound()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        AuthenticateAdmin(factory, client);

        using var response = await client.PutAsJsonAsync(
            "/api/appointment-types/999",
            new
            {
                Name = "Specialist Consultation",
                DurationMinutes = 60,
                BookingFee = 150m
            },
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
