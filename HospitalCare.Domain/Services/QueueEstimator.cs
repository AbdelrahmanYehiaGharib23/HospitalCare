using HospitalCare.Domain.Enums;

namespace HospitalCare.Domain.Services;

public sealed class QueueEstimator
{
    public IReadOnlyList<QueueEstimate> Estimate(
        DateTimeOffset scheduleStart,
        DateTimeOffset now,
        IReadOnlyCollection<QueueEstimatorAppointment> appointments)
    {
        var orderedAppointments = appointments
            .Where(appointment =>
                appointment.Status is AppointmentStatus.Completed or
                    AppointmentStatus.Queued or
                    AppointmentStatus.InProgress)
            .OrderBy(appointment => appointment.QueuePosition)
            .ToArray();
        var estimates = new List<QueueEstimate>();
        var scheduledStartTime = scheduleStart;
        var estimatedStartTime = scheduleStart;

        foreach (var appointment in orderedAppointments)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
                appointment.DurationMinutes);

            if (appointment.Status == AppointmentStatus.Completed)
            {
                estimatedStartTime = EstimateCompletedAppointmentEnd(
                    appointment,
                    scheduledStartTime,
                    estimatedStartTime);
                scheduledStartTime = scheduledStartTime.AddMinutes(
                    appointment.DurationMinutes);
                continue;
            }

            if (appointment.Status == AppointmentStatus.InProgress)
            {
                var appointmentStartTime = appointment.ActualStartTime.HasValue
                    ? Max(estimatedStartTime, appointment.ActualStartTime.Value)
                    : Max(estimatedStartTime, scheduledStartTime);
                var estimatedEndTime = Max(
                    appointmentStartTime.AddMinutes(appointment.DurationMinutes),
                    now);
                var delayMinutes = GetDelayMinutes(
                    appointmentStartTime,
                    scheduledStartTime);

                estimates.Add(new QueueEstimate(
                    appointment.Id,
                    appointment.QueuePosition,
                    appointment.Status,
                    appointmentStartTime,
                    delayMinutes));
                estimatedStartTime = estimatedEndTime;
                scheduledStartTime = scheduledStartTime.AddMinutes(
                    appointment.DurationMinutes);
                continue;
            }

            var queuedStartTime = Max(
                Max(estimatedStartTime, scheduledStartTime),
                now);
            estimates.Add(new QueueEstimate(
                appointment.Id,
                appointment.QueuePosition,
                appointment.Status,
                queuedStartTime,
                GetDelayMinutes(queuedStartTime, scheduledStartTime)));
            scheduledStartTime = scheduledStartTime.AddMinutes(
                appointment.DurationMinutes);
            estimatedStartTime = queuedStartTime.AddMinutes(
                appointment.DurationMinutes);
        }

        return estimates;
    }

    private static DateTimeOffset EstimateCompletedAppointmentEnd(
        QueueEstimatorAppointment appointment,
        DateTimeOffset scheduledStart,
        DateTimeOffset estimatedStart)
    {
        var estimatedStartTime = appointment.ActualStartTime.HasValue
            ? Max(estimatedStart, appointment.ActualStartTime.Value)
            : Max(estimatedStart, scheduledStart);

        return appointment.ActualEndTime.HasValue
            ? Max(estimatedStartTime, appointment.ActualEndTime.Value)
            : estimatedStartTime.AddMinutes(appointment.DurationMinutes);
    }

    private static int GetDelayMinutes(
        DateTimeOffset estimatedStartTime,
        DateTimeOffset scheduledStartTime)
    {
        var delay = estimatedStartTime - scheduledStartTime;
        return delay <= TimeSpan.Zero
            ? 0
            : (int)Math.Ceiling(delay.TotalMinutes);
    }

    private static DateTimeOffset Max(
        DateTimeOffset first,
        DateTimeOffset second)
    {
        return first >= second ? first : second;
    }
}
