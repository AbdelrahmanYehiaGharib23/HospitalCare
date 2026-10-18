
namespace HospitalCare.Infrastructure.Authentication
{
    public sealed class JwtOptions
    {
        public const string SectionName = "Jwt";

        public string Issuer { get; set; } = string.Empty;

        public string Audience { get; set; } = string.Empty;

        public string SecretKey { get; set; } = string.Empty;

        public int ExpirationMinutes { get; set; } = 30;
    }
}
