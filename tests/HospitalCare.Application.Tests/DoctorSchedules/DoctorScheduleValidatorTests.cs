using HospitalCare.Application.Features.DoctorSchedules.Commands;
using HospitalCare.Application.Features.DoctorSchedules.Queries;

namespace HospitalCare.Application.Tests.DoctorSchedules;

public sealed class DoctorScheduleValidatorTests
{
    private readonly UpdateDoctorScheduleCommandValidator _validator = new();

    [Fact]
    public void Validate_EmptySchedule_Succeeds()
    {
        var result = _validator.Validate(
            new UpdateDoctorScheduleCommand([]));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_NonOverlappingRangesOnSameDay_Succeeds()
    {
        var result = _validator.Validate(
            new UpdateDoctorScheduleCommand(
            [
                new DoctorScheduleRange(
                    DayOfWeek.Monday,
                    new TimeOnly(9, 0),
                    new TimeOnly(12, 0)),
                new DoctorScheduleRange(
                    DayOfWeek.Monday,
                    new TimeOnly(12, 0),
                    new TimeOnly(15, 0))
            ]));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_EndTimeBeforeStartTime_Fails()
    {
        var result = _validator.Validate(
            new UpdateDoctorScheduleCommand(
            [
                new DoctorScheduleRange(
                    DayOfWeek.Monday,
                    new TimeOnly(12, 0),
                    new TimeOnly(9, 0))
            ]));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_OverlappingRangesOnSameDay_Fails()
    {
        var result = _validator.Validate(
            new UpdateDoctorScheduleCommand(
            [
                new DoctorScheduleRange(
                    DayOfWeek.Monday,
                    new TimeOnly(9, 0),
                    new TimeOnly(12, 0)),
                new DoctorScheduleRange(
                    DayOfWeek.Monday,
                    new TimeOnly(11, 0),
                    new TimeOnly(14, 0))
            ]));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_InvalidDayOfWeek_Fails()
    {
        var result = _validator.Validate(
            new UpdateDoctorScheduleCommand(
            [
                new DoctorScheduleRange(
                    (DayOfWeek)7,
                    new TimeOnly(9, 0),
                    new TimeOnly(12, 0))
            ]));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void GetDoctorScheduleQuery_InvalidDoctorId_Fails()
    {
        var validator = new GetDoctorScheduleQueryValidator();

        var result = validator.Validate(new GetDoctorScheduleQuery(0));

        Assert.False(result.IsValid);
    }
}
