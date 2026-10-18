using HospitalCare.Domain.Entities;

namespace HospitalCare.Application.Features.DoctorSchedules.DTOs
{
    public sealed record DoctorScheduleDto(int Id, int DoctorId, DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime)
    {
        public static DoctorScheduleDto FromEntity(
            DoctorSchedule schedule)
        {
            return new DoctorScheduleDto(
                schedule.Id,
                schedule.DoctorId,
                schedule.DayOfWeek,
                schedule.StartTime,
                schedule.EndTime);
        }
    }
}
