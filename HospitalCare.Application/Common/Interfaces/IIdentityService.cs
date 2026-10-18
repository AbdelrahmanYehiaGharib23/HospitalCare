
using HospitalCare.Application.Common.Models;

namespace HospitalCare.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<JwtTokenResult> RegisterAsync(string name,string email,string password,string phone,DateOnly dateOfBirth,string? allergies = null,string? chronicConditions = null,CancellationToken cancellationToken = default);

    Task<JwtTokenResult> LoginAsync(string email,string password, CancellationToken cancellationToken = default);
}
