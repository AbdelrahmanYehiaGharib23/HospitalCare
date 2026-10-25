using HospitalCare.Domain.Entities;
using HospitalCare.Domain.Enums;

namespace HospitalCare.Domain.Services;

public sealed class DoctorCapacityCalculator
{
    public DoctorCapacityResult Calculate(
        DateOnly appointmentDate,
        IReadOnlyCollection<DoctorSchedule> schedules,
        IReadOnlyCollection<DoctorCapacityAppointment> appointments,
        int requestedDurationMinutes,
        DateTimeOffset hospitalLocalNow)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            requestedDurationMinutes);

        var today = DateOnly.FromDateTime(hospitalLocalNow.DateTime);
        var currentTime = TimeOnly.FromDateTime(hospitalLocalNow.DateTime);
        var availableMinutes = GetAvailableMinutes(
            appointmentDate,
            schedules,
            today,
            currentTime);
        var pendingMinutes = appointments
            .Where(appointment =>
                appointment.Date == appointmentDate &&
                appointment.Status is AppointmentStatus.Queued or
                    AppointmentStatus.InProgress)
            .Sum(appointment => appointment.DurationMinutes);

        return new DoctorCapacityResult(
            availableMinutes,
            pendingMinutes,
            pendingMinutes + requestedDurationMinutes <= availableMinutes);
    }

    private static int GetAvailableMinutes(
        DateOnly appointmentDate,
        IReadOnlyCollection<DoctorSchedule> schedules,
        DateOnly today,
        TimeOnly currentTime)
    {
        if (appointmentDate < today)
        {
            return 0;
        }

        return schedules
            .Where(schedule => schedule.DayOfWeek == appointmentDate.DayOfWeek)
            .Sum(schedule => appointmentDate == today
                ? GetRemainingRangeMinutes(schedule, currentTime)
                : GetRangeMinutes(schedule));
    }

    private static int GetRemainingRangeMinutes(
        DoctorSchedule schedule,
        TimeOnly currentTime)
    {
        var rangeStart = schedule.StartTime > currentTime
            ? schedule.StartTime
            : currentTime;

        if (schedule.EndTime <= rangeStart)
        {
            return 0;
        }

        return (int)(schedule.EndTime - rangeStart).TotalMinutes;
    }

    private static int GetRangeMinutes(DoctorSchedule schedule)
    {
        if (schedule.EndTime <= schedule.StartTime)
        {
            return 0;
        }

        return (int)(schedule.EndTime - schedule.StartTime).TotalMinutes;
    }
}
