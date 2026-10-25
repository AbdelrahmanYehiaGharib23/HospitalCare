using HospitalCare.Application.Features.Departments.Commands;

namespace HospitalCare.Application.Tests.Departments;

public sealed class DepartmentCommandValidatorTests
{
    [Fact]
    public void CreateDepartmentCommand_ValidName_Succeeds()
    {
        var validator = new CreateDepartmentCommandValidator();

        var result = validator.Validate(
            new CreateDepartmentCommand("Cardiology"));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("")]
    public void CreateDepartmentCommand_InvalidName_Fails(string name)
    {
        var validator = new CreateDepartmentCommandValidator();

        var result = validator.Validate(new CreateDepartmentCommand(name));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void UpdateDepartmentCommand_InvalidId_Fails()
    {
        var validator = new UpdateDepartmentCommandValidator();

        var result = validator.Validate(new UpdateDepartmentCommand(0, "Cardiology"));

        Assert.False(result.IsValid);
    }
}
