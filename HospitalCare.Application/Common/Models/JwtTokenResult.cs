namespace HospitalCare.Application.Common.Models;

public class JwtTokenResult
{
    public string Token { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
}