using HospitalCare.Domain.Entities;
using HospitalCare.Domain.Enums;

namespace HospitalCare.Application.Features.Appointments.DTOs
{
    public sealed record AppointmentDto( int Id,  int DoctorId,int PatientId,int AppointmentTypeId,int? PaymentTransactionId,DateOnly AppointmentDate,int QueuePosition,AppointmentStatus Status, DateTime? ActualStartTime ,DateTime? ActualEndTime ,string? Notes)
    {
        public static AppointmentDto FromEntity(
            Appointment appointment)
        {
            return new AppointmentDto(
                appointment.Id,
                appointment.DoctorId,
                appointment.PatientId,
                appointment.AppointmentTypeId,
                appointment.PaymentTransactionId,
                appointment.AppointmentDate,
                appointment.QueuePosition,
                appointment.Status,
                appointment.ActualStartTime,
                appointment.ActualEndTime,
                appointment.Notes);
        }
    }
}