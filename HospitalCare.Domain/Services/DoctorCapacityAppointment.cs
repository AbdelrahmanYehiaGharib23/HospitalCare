using HospitalCare.Domain.Enums;

namespace HospitalCare.Domain.Services;

public sealed record DoctorCapacityAppointment(
    DateOnly Date,
    AppointmentStatus Status,
    int DurationMinutes);
