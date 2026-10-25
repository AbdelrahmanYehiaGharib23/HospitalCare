using HospitalCare.Application.Features.AppointmentTypes.Commands;

namespace HospitalCare.Application.Tests.AppointmentTypes;

public sealed class AppointmentTypeCommandValidatorTests
{
    [Fact]
    public void CreateAppointmentTypeCommand_ValidValues_Succeeds()
    {
        var validator = new CreateAppointmentTypeCommandValidator();

        var result = validator.Validate(
            new CreateAppointmentTypeCommand("Consultation", 30, 100m));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(" ", 30, 100)]
    [InlineData("Consultation", 0, 100)]
    [InlineData("Consultation", 30, 0)]
    [InlineData("Consultation", 30, 100.123)]
    public void CreateAppointmentTypeCommand_InvalidValues_Fails(
        string name,
        int durationMinutes,
        decimal bookingFee)
    {
        var validator = new CreateAppointmentTypeCommandValidator();

        var result = validator.Validate(
            new CreateAppointmentTypeCommand(name, durationMinutes, bookingFee));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void UpdateAppointmentTypeCommand_ValidValues_Succeeds()
    {
        var validator = new UpdateAppointmentTypeCommandValidator();

        var result = validator.Validate(
            new UpdateAppointmentTypeCommand(1, "Follow-up", 15, 50m));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void UpdateAppointmentTypeCommand_InvalidId_Fails()
    {
        var validator = new UpdateAppointmentTypeCommandValidator();

        var result = validator.Validate(
            new UpdateAppointmentTypeCommand(0, "Follow-up", 15, 50m));

        Assert.False(result.IsValid);
    }
}
