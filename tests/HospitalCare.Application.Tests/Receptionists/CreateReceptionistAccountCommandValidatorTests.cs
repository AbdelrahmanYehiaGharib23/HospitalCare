using HospitalCare.Application.Features.Receptionists.Commands;

namespace HospitalCare.Application.Tests.Receptionists;

public sealed class CreateReceptionistAccountCommandValidatorTests
{
    private readonly CreateReceptionistAccountCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_Succeeds()
    {
        var result = _validator.Validate(
            new CreateReceptionistAccountCommand(
                "receptionist@example.com",
                "Password123!"));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("", "Password123!")]
    [InlineData("invalid-email", "Password123!")]
    [InlineData("receptionist@example.com", "")]
    public void Validate_InvalidCommand_Fails(string email, string password)
    {
        var result = _validator.Validate(
            new CreateReceptionistAccountCommand(email, password));

        Assert.False(result.IsValid);
    }
}
