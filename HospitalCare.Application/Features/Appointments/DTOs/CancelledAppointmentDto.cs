using HospitalCare.Domain.Entities;
using HospitalCare.Domain.Enums;

namespace HospitalCare.Application.Features.Appointments.DTOs;

public sealed record CancelledAppointmentDto(
    int Id,
    int DoctorId,
    DateOnly AppointmentDate,
    int QueuePosition,
    AppointmentStatus Status,
    PaymentStatus PaymentStatus)
{
    public static CancelledAppointmentDto FromEntities(
        Appointment appointment,
        PaymentTransaction paymentTransaction)
    {
        return new CancelledAppointmentDto(
            appointment.Id,
            appointment.DoctorId,
            appointment.AppointmentDate,
            appointment.QueuePosition,
            appointment.Status,
            paymentTransaction.Status);
    }
}
