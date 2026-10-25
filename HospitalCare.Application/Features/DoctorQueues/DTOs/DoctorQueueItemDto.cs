using HospitalCare.Domain.Enums;

namespace HospitalCare.Application.Features.DoctorQueues.DTOs;

public sealed record DoctorQueueItemDto(
    int AppointmentId,
    int PatientId,
    string PatientName,
    int AppointmentTypeId,
    int QueuePosition,
    AppointmentStatus Status,
    DateTimeOffset? EstimatedStartTime,
    int? EstimatedDelayMinutes,
    DateTime? CalledAt,
    DateTime? CheckedInAt);
