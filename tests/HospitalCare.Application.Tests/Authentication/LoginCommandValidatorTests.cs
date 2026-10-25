using HospitalCare.Application.Authentication;

namespace HospitalCare.Application.Tests.Authentication;

public sealed class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidLoginCommand_Succeeds()
    {
        var result = _validator.Validate(
            new LoginCommand("patient@example.com", "Password1!"));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid-email")]
    public void Validate_InvalidEmail_Fails(string email)
    {
        var result = _validator.Validate(
            new LoginCommand(email, "Password1!"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_MissingPassword_Fails()
    {
        var result = _validator.Validate(
            new LoginCommand("patient@example.com", string.Empty));

        Assert.False(result.IsValid);
    }
}
