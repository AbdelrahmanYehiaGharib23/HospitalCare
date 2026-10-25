namespace HospitalCare.Application.Features.DoctorSchedules.Commands;

public sealed record DoctorScheduleRange(
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime);
