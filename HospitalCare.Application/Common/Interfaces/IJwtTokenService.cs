using HospitalCare.Application.Common.Models;

namespace HospitalCare.Application.Common.Interfaces;

public interface IJwtTokenService
{
    JwtTokenResult GenerateToken(
        string userId,
        string email,
        IEnumerable<string> roles,
        string? patientId = null);
}
