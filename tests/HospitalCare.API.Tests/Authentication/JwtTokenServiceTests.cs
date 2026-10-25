using HospitalCare.Infrastructure.Authentication;
using Microsoft.Extensions.Options;

namespace HospitalCare.API.Tests.Authentication;

public sealed class JwtTokenServiceTests
{
    [Fact]
    public void GenerateToken_UsesInjectedTimeProviderForExpiration()
    {
        var now = new DateTimeOffset(
            2030,
            3,
            1,
            12,
            30,
            0,
            TimeSpan.Zero);
        var service = new JwtTokenService(
            Options.Create(new JwtOptions
            {
                Issuer = "HospitalCare.API.Tests",
                Audience = "HospitalCare.API.Tests",
                SecretKey = "HospitalCare test JWT key with enough length",
                ExpirationMinutes = 45
            }),
            new FixedTimeProvider(now));

        var token = service.GenerateToken(
            "user-1",
            "patient@example.com",
            ["Patient"]);

        Assert.Equal(now.AddMinutes(45).UtcDateTime, token.ExpiresAt);
        Assert.False(string.IsNullOrWhiteSpace(token.Token));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
