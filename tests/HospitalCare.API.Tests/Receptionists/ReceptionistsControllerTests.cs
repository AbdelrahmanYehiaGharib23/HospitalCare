using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HospitalCare.API.Tests.Infrastructure;
using HospitalCare.Application.Common.Constants;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Common.Models;
using HospitalCare.Application.Features.Receptionists.DTOs;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalCare.API.Tests.Receptionists;

public sealed class ReceptionistsControllerTests
{
    [Fact]
    public async Task CreateReceptionist_AdminCreatesAccount_ReceptionistCanLogin()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        AuthenticateAdmin(factory, client);
        var email = $"receptionist-{Guid.NewGuid():N}@example.com";

        using var createResponse = await client.PostAsJsonAsync(
            "/api/receptionists",
            new { Email = email, Password = "Password123!" },
            TestContext.Current.CancellationToken);
        var account = await createResponse.Content
            .ReadFromJsonAsync<ReceptionistAccountDto>(
                TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        Assert.NotNull(account);
        Assert.Equal(email, account.Email);
        Assert.Equal(Roles.Receptionist, account.Role);

        using var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { Email = email, Password = "Password123!" },
            TestContext.Current.CancellationToken);
        var token = await loginResponse.Content
            .ReadFromJsonAsync<JwtTokenResult>(
                TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.NotNull(token);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.Token);
        using var deniedResponse = await client.PostAsJsonAsync(
            "/api/receptionists",
            new
            {
                Email = $"another-{Guid.NewGuid():N}@example.com",
                Password = "Password123!"
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, deniedResponse.StatusCode);
    }

    [Fact]
    public async Task CreateReceptionist_DuplicateEmail_ReturnsConflict()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        AuthenticateAdmin(factory, client);
        var request = new
        {
            Email = $"receptionist-{Guid.NewGuid():N}@example.com",
            Password = "Password123!"
        };

        using var firstResponse = await client.PostAsJsonAsync(
            "/api/receptionists",
            request,
            TestContext.Current.CancellationToken);
        using var duplicateResponse = await client.PostAsJsonAsync(
            "/api/receptionists",
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
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
}
