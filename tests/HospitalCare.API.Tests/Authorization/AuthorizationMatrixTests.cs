using System.Net;
using System.Net.Http.Headers;
using System.Text;
using HospitalCare.API.Tests.Infrastructure;
using HospitalCare.Application.Common.Constants;
using HospitalCare.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalCare.API.Tests.Authorization;

public sealed class AuthorizationMatrixTests
{
    private static readonly string[] RoleNames =
    [
        Roles.Admin,
        Roles.Doctor,
        Roles.Receptionist,
        Roles.Patient
    ];

    [Fact]
    public async Task ProtectedEndpoints_RoleMatrix_RequiresAllowedRole()
    {
        using var factory = new AuthorizationMatrixApiFactory();
        using var client = factory.CreateClient();
        var endpoints = GetEndpoints();
        var cancellationToken = TestContext.Current.CancellationToken;

        foreach (var endpoint in endpoints)
        {
            client.DefaultRequestHeaders.Authorization = null;
            using var anonymousResponse = await SendAsync(
                client,
                endpoint,
                cancellationToken);
            Assert.Equal(
                HttpStatusCode.Unauthorized,
                anonymousResponse.StatusCode);

            foreach (var role in RoleNames)
            {
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue(
                        "Bearer",
                        CreateToken(factory, role));
                using var response = await SendAsync(
                    client,
                    endpoint,
                    cancellationToken);
                if (endpoint.AllowedRoles.Contains(role))
                {
                    Assert.True(
                        response.IsSuccessStatusCode,
                        $"{role} received {(int)response.StatusCode} for " +
                        $"{endpoint.Method} {endpoint.Path}; expected 2xx.");
                }
                else
                {
                    Assert.Equal(
                        HttpStatusCode.Forbidden,
                        response.StatusCode);
                }
            }
        }
    }

    private static IReadOnlyList<ProtectedEndpoint> GetEndpoints()
    {
        return
        [
            new(
                HttpMethod.Get,
                "/api/patients/me",
                null,
                Roles.Patient),
            new(
                HttpMethod.Put,
                "/api/patients/me",
                "{\"name\":\"Patient\",\"phone\":\"01000000000\",\"dateOfBirth\":\"1990-01-01\",\"allergies\":null,\"chronicConditions\":null}",
                Roles.Patient),
            new(
                HttpMethod.Get,
                "/api/patients/me/medical-history",
                null,
                Roles.Patient),
            new(
                HttpMethod.Get,
                "/api/patients/1/medical-history",
                null,
                Roles.Doctor,
                Roles.Receptionist),
            new(HttpMethod.Get, "/api/departments", null, Roles.Patient),
            new(
                HttpMethod.Post,
                "/api/departments",
                """{"name":"Department"}""",
                Roles.Admin),
            new(
                HttpMethod.Put,
                "/api/departments/1",
                """{"name":"Department"}""",
                Roles.Admin),
            new(
                HttpMethod.Post,
                "/api/departments/1/deactivate",
                null,
                Roles.Admin),
            new(
                HttpMethod.Get,
                "/api/doctors",
                null,
                Roles.Admin,
                Roles.Patient),
            new(
                HttpMethod.Get,
                "/api/doctors/1",
                null,
                Roles.Admin,
                Roles.Patient),
            new(
                HttpMethod.Post,
                "/api/doctors",
                """{"name":"Doctor","email":"doctor@example.com","password":"Password1!","departmentId":1}""",
                Roles.Admin),
            new(
                HttpMethod.Post,
                "/api/doctors/1/deactivate",
                null,
                Roles.Admin),
            new(
                HttpMethod.Get,
                "/api/doctors/1/schedule",
                null,
                Roles.Admin,
                Roles.Patient),
            new(
                HttpMethod.Get,
                "/api/doctors/me/schedule",
                null,
                Roles.Doctor),
            new(
                HttpMethod.Get,
                "/api/doctors/1/available-dates?appointmentTypeId=1&from=2030-01-01&to=2030-01-01",
                null,
                Roles.Patient),
            new(
                HttpMethod.Get,
                "/api/doctors/1/queue?date=2030-01-01",
                null,
                Roles.Receptionist),
            new(
                HttpMethod.Get,
                "/api/doctors/me/queue?date=2030-01-01",
                null,
                Roles.Doctor),
            new(
                HttpMethod.Post,
                "/api/doctors/me/queue/call-next?date=2030-01-01",
                null,
                Roles.Doctor),
            new(
                HttpMethod.Post,
                "/api/doctors/1/queue/call-next?date=2030-01-01",
                null,
                Roles.Receptionist),
            new(
                HttpMethod.Put,
                "/api/doctors/me/schedule",
                """{"ranges":[]}""",
                Roles.Doctor),
            new(
                HttpMethod.Get,
                "/api/appointment-types",
                null,
                Roles.Admin,
                Roles.Patient),
            new(
                HttpMethod.Post,
                "/api/appointment-types",
                """{"name":"Consultation","durationMinutes":30,"bookingFee":100}""",
                Roles.Admin),
            new(
                HttpMethod.Put,
                "/api/appointment-types/1",
                """{"name":"Consultation","durationMinutes":30,"bookingFee":100}""",
                Roles.Admin),
            new(
                HttpMethod.Post,
                "/api/receptionists",
                """{"email":"receptionist@example.com","password":"Password1!"}""",
                Roles.Admin),
            new(
                HttpMethod.Post,
                "/api/appointments/payments",
                """{"doctorId":1,"appointmentTypeId":1,"requestedDate":"2030-01-01"}""",
                Roles.Patient),
            new(
                HttpMethod.Post,
                "/api/appointments/1/cancel",
                null,
                Roles.Admin,
                Roles.Receptionist,
                Roles.Patient),
            new(
                HttpMethod.Post,
                "/api/appointments/1/check-in",
                null,
                Roles.Receptionist),
            new(
                HttpMethod.Post,
                "/api/appointments/1/start",
                null,
                Roles.Doctor),
            new(
                HttpMethod.Post,
                "/api/appointments/1/complete",
                """{"notes":"Completed"}""",
                Roles.Doctor),
            new(HttpMethod.Get, "/api/appointments/me", null, Roles.Patient),
            new(HttpMethod.Get, "/api/appointments/1", null, Roles.Patient)
        ];
    }

    private static string CreateToken(
        HospitalCareApiFactory factory,
        string role)
    {
        using var scope = factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider
            .GetRequiredService<IJwtTokenService>();
        return tokenService.GenerateToken(
            $"{role.ToLowerInvariant()}-user",
            $"{role.ToLowerInvariant()}@example.com",
            [role],
            patientId: role == Roles.Patient ? "1" : null,
            doctorId: role == Roles.Doctor ? "1" : null).Token;
    }

    private static Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        ProtectedEndpoint endpoint,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(endpoint.Method, endpoint.Path);
        if (endpoint.Body is not null)
        {
            request.Content = new StringContent(
                endpoint.Body,
                Encoding.UTF8,
                "application/json");
        }

        return SendRequestAsync(client, request, cancellationToken);
    }

    private static async Task<HttpResponseMessage> SendRequestAsync(
        HttpClient client,
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        using (request)
        {
            return await client.SendAsync(request, cancellationToken);
        }
    }

    private sealed record ProtectedEndpoint(
        HttpMethod Method,
        string Path,
        string? Body,
        params string[] AllowedRoles);
}
