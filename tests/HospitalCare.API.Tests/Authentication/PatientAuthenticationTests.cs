using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HospitalCare.API.Tests.Infrastructure;
using HospitalCare.Application.Authentication;
using HospitalCare.Application.Common.Models;
using HospitalCare.Application.Features.Patients.DTOs;

namespace HospitalCare.API.Tests.Authentication;

public sealed class PatientAuthenticationTests
{
    [Fact]
    public async Task GetMyProfile_AnonymousUser_ReturnsUnauthorized()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            "/api/patients/me",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_InvalidCredentials_ReturnsProblemDetails()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginCommand("patient@example.com", "Password1!"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        using var problem = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(
                TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(401, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(
            "Unauthorized",
            problem.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Register_InvalidCommand_ReturnsValidationProblemDetails()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterPatientCommand(
                string.Empty,
                "invalid-email",
                string.Empty,
                string.Empty,
                new DateOnly(1990, 1, 1)),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        using var problem = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(
                TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(400, problem.RootElement.GetProperty("status").GetInt32());
        Assert.True(
            problem.RootElement.GetProperty("errors").EnumerateObject().Any());
    }

    [Fact]
    public async Task RegisterAndLogin_PatientCanReadOwnProfile()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;
        const string email = "sam@example.com";
        const string password = "Password1!";

        var registrationResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterPatientCommand(
                "Sam Example",
                email,
                password,
                "01000000000",
                new DateOnly(1990, 1, 1)),
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, registrationResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginCommand(email, password),
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var tokenResult = await loginResponse.Content.ReadFromJsonAsync<JwtTokenResult>(
            cancellationToken);
        Assert.NotNull(tokenResult);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokenResult.Token);

        var profileResponse = await client.GetAsync(
            "/api/patients/me",
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);
        var profile = await profileResponse.Content.ReadFromJsonAsync<PatientDto>(
            cancellationToken);

        Assert.NotNull(profile);
        Assert.Equal("Sam Example", profile.Name);
        Assert.Equal(email, profile.Email);
    }
}
