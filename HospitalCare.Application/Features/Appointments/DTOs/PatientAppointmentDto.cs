using HospitalCare.Domain.Entities;
using HospitalCare.Domain.Enums;

namespace HospitalCare.Application.Features.Appointments.DTOs;

public sealed record PatientAppointmentDto(
    int Id,
    int DoctorId,
    int AppointmentTypeId,
    DateOnly AppointmentDate,
    int? QueuePosition,
    AppointmentStatus Status,
    DateTime? ActualStartTime,
    DateTime? ActualEndTime,
    string? Notes)
{
    public static PatientAppointmentDto FromEntity(
        Appointment appointment,
        int? queuePosition)
    {
        return new PatientAppointmentDto(
            appointment.Id,
            appointment.DoctorId,
            appointment.AppointmentTypeId,
            appointment.AppointmentDate,
            queuePosition,
            appointment.Status,
            appointment.ActualStartTime,
            appointment.ActualEndTime,
            appointment.Notes);
    }
}
