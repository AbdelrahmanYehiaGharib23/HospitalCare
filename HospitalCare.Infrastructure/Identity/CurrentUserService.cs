using System.Security.Claims;
using HospitalCare.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace HospitalCare.Infrastructure.Identity;

public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User =>
        _httpContextAccessor.HttpContext?.User;

    public string? UserId =>
        User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    public string? PatientId =>
        User?.FindFirst("patientId")?.Value;

    public string? DoctorId =>
        User?.FindFirst("doctorId")?.Value;

    public string? Email =>
        User?.FindFirst(ClaimTypes.Email)?.Value
        ?? User?.FindFirst("email")?.Value;

    public bool IsAuthenticated =>
        User?.Identity?.IsAuthenticated ?? false;

    public bool IsInRole(string role) =>
        User?.IsInRole(role) ?? false;
}
