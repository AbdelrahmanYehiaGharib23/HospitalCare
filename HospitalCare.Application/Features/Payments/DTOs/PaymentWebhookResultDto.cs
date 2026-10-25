using HospitalCare.Domain.Enums;

namespace HospitalCare.Application.Features.Payments.DTOs;

public sealed record PaymentWebhookResultDto(
    int PaymentTransactionId,
    PaymentStatus Status,
    int? AppointmentId,
    int? QueuePosition);
