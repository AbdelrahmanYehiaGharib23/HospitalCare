namespace HospitalCare.Application.Common.Models;

public sealed record PaymentCheckoutRequest(
    int PaymentTransactionId,
    decimal Amount,
    string Currency);
