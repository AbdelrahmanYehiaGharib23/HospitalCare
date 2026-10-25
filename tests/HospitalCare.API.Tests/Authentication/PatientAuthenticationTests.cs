using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
