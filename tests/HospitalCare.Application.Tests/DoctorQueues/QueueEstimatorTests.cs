using HospitalCare.Domain.Enums;
using HospitalCare.Domain.Services;

namespace HospitalCare.Application.Tests.DoctorQueues;

public sealed class QueueEstimatorTests
{
    private readonly QueueEstimator _queueEstimator = new();

    [Fact]
    public void Estimate_FutureQueue_UsesScheduleAndAppointmentDurations()
    {
        var scheduleStart = Time(2026, 10, 26, 9, 0);
        var estimates = _queueEstimator.Estimate(
            scheduleStart,
            Time(2026, 10, 26, 8, 0),
            [
                Appointment(1, 1, AppointmentStatus.Queued, 30),
                Appointment(2, 2, AppointmentStatus.Queued, 15)
            ]);

        Assert.Equal(Time(2026, 10, 26, 9, 0), estimates[0].EstimatedStartTime);
        Assert.Equal(0, estimates[0].EstimatedDelayMinutes);
        Assert.Equal(Time(2026, 10, 26, 9, 30), estimates[1].EstimatedStartTime);
    }

    [Fact]
    public void Estimate_CompletedAppointmentStartedLate_ShiftsFollowingEstimates()
    {
        var scheduleStart = Time(2026, 10, 26, 9, 0);
        var estimates = _queueEstimator.Estimate(
            scheduleStart,
            Time(2026, 10, 26, 9, 45),
            [
                Appointment(
                    1,
                    1,
                    AppointmentStatus.Completed,
                    30,
                    Time(2026, 10, 26, 9, 10),
                    Time(2026, 10, 26, 9, 40)),
                Appointment(2, 2, AppointmentStatus.Queued, 30),
                Appointment(3, 3, AppointmentStatus.Queued, 15)
            ]);

        Assert.Equal(Time(2026, 10, 26, 9, 45), estimates[0].EstimatedStartTime);
        Assert.Equal(15, estimates[0].EstimatedDelayMinutes);
        Assert.Equal(Time(2026, 10, 26, 10, 15), estimates[1].EstimatedStartTime);
        Assert.Equal(15, estimates[1].EstimatedDelayMinutes);
    }

    [Fact]
    public void Estimate_InProgressAppointmentOverrun_DelaysNextPatientFromNow()
    {
        var estimates = _queueEstimator.Estimate(
            Time(2026, 10, 26, 9, 0),
            Time(2026, 10, 26, 9, 40),
            [
                Appointment(
                    1,
                    1,
                    AppointmentStatus.InProgress,
                    30,
                    Time(2026, 10, 26, 9, 0)),
                Appointment(2, 2, AppointmentStatus.Queued, 15)
            ]);

        Assert.Equal(Time(2026, 10, 26, 9, 40), estimates[1].EstimatedStartTime);
        Assert.Equal(10, estimates[1].EstimatedDelayMinutes);
    }

    [Fact]
    public void Estimate_CancelledAndNoShowAppointments_DoNotDelayQueue()
    {
        var estimates = _queueEstimator.Estimate(
            Time(2026, 10, 26, 9, 0),
            Time(2026, 10, 26, 8, 0),
            [
                Appointment(1, 1, AppointmentStatus.Cancelled, 30),
                Appointment(2, 2, AppointmentStatus.NoShowPenalized, 30),
                Appointment(3, 3, AppointmentStatus.Queued, 15)
            ]);

        var estimate = Assert.Single(estimates);
        Assert.Equal(3, estimate.AppointmentId);
        Assert.Equal(Time(2026, 10, 26, 9, 0), estimate.EstimatedStartTime);
    }

    private static QueueEstimatorAppointment Appointment(
        int id,
        int queuePosition,
        AppointmentStatus status,
        int durationMinutes,
        DateTimeOffset? actualStart = null,
        DateTimeOffset? actualEnd = null)
    {
        return new QueueEstimatorAppointment(
            id,
            queuePosition,
            status,
            durationMinutes,
            actualStart,
            actualEnd);
    }

    private static DateTimeOffset Time(
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
