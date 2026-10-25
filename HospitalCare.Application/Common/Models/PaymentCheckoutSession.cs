namespace HospitalCare.Application.Common.Models;

public sealed record PaymentCheckoutSession(
    string GatewayReference,
    string CheckoutUrl);
