namespace HospitalCare.Application.Common.Models;

public sealed record PaymentRefundRequest(
    int PaymentTransactionId,
    string GatewayReference,
    decimal Amount,
    string Currency);
