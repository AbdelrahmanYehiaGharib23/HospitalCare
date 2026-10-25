using HospitalCare.Application.Authentication;

namespace HospitalCare.Application.Tests.Authentication;

public sealed class RegisterPatientCommandValidatorTests
{
    private readonly RegisterPatientCommandValidator _validator = new(
        new FixedTimeProvider(new DateTimeOffset(
            2030,
            3,
            1,
            0,
            0,
            0,
            TimeSpan.Zero)));

    [Fact]
    public void Validate_PastDateOfBirth_Succeeds()
    {
        var result = _validator.Validate(CreateCommand());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_TodayDateOfBirth_Fails()
    {
        var result = _validator.Validate(
            CreateCommand() with { DateOfBirth = new DateOnly(2030, 3, 1) });

        Assert.False(result.IsValid);
    }

    private static RegisterPatientCommand CreateCommand()
    {
        return new RegisterPatientCommand(
            "Sam Example",
            "sam@example.com",
            "Password123!",
            "01000000000",
            new DateOnly(1990, 1, 1));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
