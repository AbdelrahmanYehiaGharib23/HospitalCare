using HospitalCare.Domain.Enums;

namespace HospitalCare.Domain.Services;

public sealed record QueueEstimatorAppointment(
    int Id,
    int QueuePosition,
    AppointmentStatus Status,
    int DurationMinutes,
    DateTimeOffset? ActualStartTime,
    DateTimeOffset? ActualEndTime);
