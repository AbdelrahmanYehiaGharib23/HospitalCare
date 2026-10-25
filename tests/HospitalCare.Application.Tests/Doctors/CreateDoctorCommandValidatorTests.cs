using HospitalCare.Application.Features.Doctors.Commands;

namespace HospitalCare.Application.Tests.Doctors;

public sealed class CreateDoctorCommandValidatorTests
{
    private readonly CreateDoctorCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_Succeeds()
    {
        var result = _validator.Validate(
            new CreateDoctorCommand(
                "Dr. Example",
                "doctor@example.com",
                "Password123!",
                1));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("", "doctor@example.com", "Password123!", 1)]
    [InlineData("Dr. Example", "invalid-email", "Password123!", 1)]
    [InlineData("Dr. Example", "doctor@example.com", "", 1)]
    [InlineData("Dr. Example", "doctor@example.com", "Password123!", 0)]
    public void Validate_InvalidCommand_Fails(
        string name,
        string email,
        string password,
        int departmentId)
    {
        var result = _validator.Validate(
            new CreateDoctorCommand(name, email, password, departmentId));

        Assert.False(result.IsValid);
    }
}
