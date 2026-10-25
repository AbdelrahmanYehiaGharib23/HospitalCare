using HospitalCare.Application.Features.DoctorSchedules.Queries;

namespace HospitalCare.Application.Tests.DoctorCapacity;

public sealed class GetDoctorAvailableDatesQueryValidatorTests
{
    [Fact]
    public void Validate_ValidQuery_Succeeds()
    {
        var validator = new GetDoctorAvailableDatesQueryValidator();
        var from = new DateOnly(2026, 6, 1);

        var result = validator.Validate(
            new GetDoctorAvailableDatesQuery(1, 1, from, from.AddDays(14)));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public void Validate_InvalidIdentifiers_Fails(
        int doctorId,
        int appointmentTypeId)
    {
        var validator = new GetDoctorAvailableDatesQueryValidator();
        var date = new DateOnly(2026, 6, 1);

        var result = validator.Validate(
            new GetDoctorAvailableDatesQuery(
                doctorId,
                appointmentTypeId,
                date,
                date));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_FromAfterTo_Fails()
    {
        var validator = new GetDoctorAvailableDatesQueryValidator();

        var result = validator.Validate(
            new GetDoctorAvailableDatesQuery(
                1,
                1,
                new DateOnly(2026, 6, 2),
                new DateOnly(2026, 6, 1)));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_LongRequestedRange_Succeeds()
    {
        var validator = new GetDoctorAvailableDatesQueryValidator();
        var from = new DateOnly(2026, 6, 1);

        var result = validator.Validate(
            new GetDoctorAvailableDatesQuery(1, 1, from, from.AddDays(365)));

        Assert.True(result.IsValid);
    }
}
