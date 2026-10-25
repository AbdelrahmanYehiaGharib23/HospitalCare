using HospitalCare.Domain.Entities;
using HospitalCare.Domain.Enums;
using HospitalCare.Domain.Services;

namespace HospitalCare.Application.Tests.DoctorCapacity;

public sealed class DoctorCapacityCalculatorTests
{
    private readonly DoctorCapacityCalculator _calculator = new();

    [Fact]
    public void Calculate_FutureDate_UsesFullSchedule()
    {
        var date = new DateOnly(2026, 6, 2);

        var result = _calculator.Calculate(
            date,
            [Schedule(date.DayOfWeek, 9, 0, 17, 0)],
            [],
            30,
            LocalNow(2026, 6, 1, 16, 0));

        Assert.Equal(480, result.AvailableMinutes);
        Assert.True(result.CanFit);
    }

    [Fact]
    public void Calculate_Today_UsesOnlyRemainingTimeInEachRange()
    {
        var date = new DateOnly(2026, 6, 1);

        var result = _calculator.Calculate(
            date,
            [
                Schedule(date.DayOfWeek, 8, 0, 10, 0),
                Schedule(date.DayOfWeek, 10, 30, 12, 0)
            ],
            [],
            60,
            LocalNow(2026, 6, 1, 10, 15));

        Assert.Equal(90, result.AvailableMinutes);
        Assert.True(result.CanFit);
    }

    [Fact]
    public void Calculate_FutureDate_SumsSeveralRanges()
    {
        var date = new DateOnly(2026, 6, 2);

        var result = _calculator.Calculate(
            date,
            [
                Schedule(date.DayOfWeek, 9, 0, 12, 0),
                Schedule(date.DayOfWeek, 13, 0, 16, 0)
            ],
            [],
            30,
            LocalNow(2026, 6, 1, 8, 0));

        Assert.Equal(360, result.AvailableMinutes);
    }

    [Fact]
    public void Calculate_QueuedAndInProgressAppointments_CountAsPending()
    {
        var date = new DateOnly(2026, 6, 2);

        var result = _calculator.Calculate(
            date,
            [Schedule(date.DayOfWeek, 9, 0, 11, 0)],
            [
                Appointment(date, AppointmentStatus.Queued, 30),
                Appointment(date, AppointmentStatus.InProgress, 15),
                Appointment(date.AddDays(1), AppointmentStatus.Queued, 45)
            ],
            30,
            LocalNow(2026, 6, 1, 8, 0));

        Assert.Equal(45, result.PendingMinutes);
        Assert.True(result.CanFit);
    }

    [Fact]
    public void Calculate_CompletedCancelledAndNoShowAppointments_DoNotCount()
    {
        var date = new DateOnly(2026, 6, 2);

        var result = _calculator.Calculate(
            date,
            [Schedule(date.DayOfWeek, 9, 0, 10, 0)],
            [
                Appointment(date, AppointmentStatus.Completed, 30),
                Appointment(date, AppointmentStatus.Cancelled, 30),
                Appointment(date, AppointmentStatus.NoShowPenalized, 30)
            ],
            60,
            LocalNow(2026, 6, 1, 8, 0));

        Assert.Equal(0, result.PendingMinutes);
        Assert.True(result.CanFit);
    }

    [Fact]
    public void Calculate_ExactFit_HasCapacity()
    {
        var date = new DateOnly(2026, 6, 2);

        var result = _calculator.Calculate(
            date,
            [Schedule(date.DayOfWeek, 9, 0, 10, 0)],
            [Appointment(date, AppointmentStatus.Queued, 30)],
            30,
            LocalNow(2026, 6, 1, 8, 0));

        Assert.Equal(60, result.AvailableMinutes);
        Assert.Equal(30, result.PendingMinutes);
        Assert.True(result.CanFit);
    }

    [Fact]
    public void Calculate_OverCapacity_HasNoCapacity()
    {
        var date = new DateOnly(2026, 6, 2);

        var result = _calculator.Calculate(
            date,
            [Schedule(date.DayOfWeek, 9, 0, 10, 0)],
            [Appointment(date, AppointmentStatus.InProgress, 31)],
            30,
            LocalNow(2026, 6, 1, 8, 0));

        Assert.False(result.CanFit);
    }

    [Fact]
    public void Calculate_NoScheduleForDate_HasNoCapacity()
    {
        var date = new DateOnly(2026, 6, 2);

        var result = _calculator.Calculate(
            date,
            [Schedule(DayOfWeek.Wednesday, 9, 0, 17, 0)],
            [],
            15,
            LocalNow(2026, 6, 1, 8, 0));

        Assert.Equal(0, result.AvailableMinutes);
        Assert.False(result.CanFit);
    }

    [Fact]
    public void Calculate_PastDate_HasNoCapacity()
    {
        var date = new DateOnly(2026, 5, 31);

        var result = _calculator.Calculate(
            date,
            [Schedule(date.DayOfWeek, 9, 0, 17, 0)],
            [],
            30,
            LocalNow(2026, 6, 1, 8, 0));

        Assert.Equal(0, result.AvailableMinutes);
        Assert.False(result.CanFit);
    }

    private static DoctorSchedule Schedule(
        DayOfWeek dayOfWeek,
        int startHour,
        int startMinute,
        int endHour,
        int endMinute)
    {
        return new DoctorSchedule
        {
            DayOfWeek = dayOfWeek,
            StartTime = new TimeOnly(startHour, startMinute),
            EndTime = new TimeOnly(endHour, endMinute)
        };
    }

    private static DoctorCapacityAppointment Appointment(
        DateOnly date,
        AppointmentStatus status,
        int durationMinutes)
    {
        return new DoctorCapacityAppointment(date, status, durationMinutes);
    }

    private static DateTimeOffset LocalNow(
        int year,
        int month,
        int day,
        int hour,
        int minute)
    {
        return new DateTimeOffset(
            year,
            month,
            day,
            hour,
            minute,
            0,
            TimeSpan.Zero);
    }
}
