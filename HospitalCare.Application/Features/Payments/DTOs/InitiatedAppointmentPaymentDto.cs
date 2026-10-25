namespace HospitalCare.Application.Features.Payments.DTOs;

public sealed record InitiatedAppointmentPaymentDto(
    int PaymentTransactionId,
    decimal Amount,
    string Currency,
    string CheckoutUrl);
