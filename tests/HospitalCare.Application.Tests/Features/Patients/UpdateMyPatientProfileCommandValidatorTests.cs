using HospitalCare.Application.Features.Patients.Commands;

namespace HospitalCare.Application.Tests.Features.Patients;

public sealed class UpdateMyPatientProfileCommandValidatorTests
{
    private readonly UpdateMyPatientProfileCommandValidator _validator = new(
        new FixedTimeProvider(new DateTimeOffset(
            2030,
            3,
            1,
            0,
            0,
            0,
            TimeSpan.Zero)));

    [Fact]
    public void Validate_ValidProfile_Succeeds()
    {
        var result = _validator.Validate(CreateCommand());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_EmptyName_Fails()
    {
        var result = _validator.Validate(
            CreateCommand() with { Name = string.Empty });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_FutureDateOfBirth_Fails()
    {
        var result = _validator.Validate(
            CreateCommand() with { DateOfBirth = new DateOnly(2999, 1, 1) });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_TodayDateOfBirth_Fails()
    {
        var result = _validator.Validate(
            CreateCommand() with { DateOfBirth = new DateOnly(2030, 3, 1) });

        Assert.False(result.IsValid);
    }

    private static UpdateMyPatientProfileCommand CreateCommand()
    {
        return new UpdateMyPatientProfileCommand(
            "Sam Example",
            "01000000000",
            new DateOnly(1990, 1, 1),
            null,
            null);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
