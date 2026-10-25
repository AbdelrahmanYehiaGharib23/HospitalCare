using HospitalCare.Domain.Entities;

namespace HospitalCare.Application.Features.AppointmentTypes.DTOs;

public sealed record AppointmentTypeDto(
    int Id,
    string Name,
    int DurationMinutes,
    decimal BookingFee)
{
    public static AppointmentTypeDto FromEntity(AppointmentType appointmentType)
    {
        return new AppointmentTypeDto(
            appointmentType.Id,
            appointmentType.Name,
            appointmentType.DurationMinutes,
            appointmentType.BookingFee);
    }
}
