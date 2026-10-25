using HospitalCare.Domain.Enums;

namespace HospitalCare.Domain.Services;

public sealed record QueueEstimate(
    int AppointmentId,
    int QueuePosition,
    AppointmentStatus Status,
    DateTimeOffset EstimatedStartTime,
    int EstimatedDelayMinutes);
