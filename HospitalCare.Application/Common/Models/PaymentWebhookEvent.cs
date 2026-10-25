namespace HospitalCare.Application.Common.Models;

public sealed record PaymentWebhookEvent(
    string GatewayReference,
    PaymentWebhookStatus Status,
    decimal Amount,
    string Currency);
