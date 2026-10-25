namespace HospitalCare.Infrastructure.Payments;

public sealed class FakePaymentGatewayOptions
{
    public const string SectionName = "PaymentGateway:Fake";

    public string WebhookSecret { get; set; } = string.Empty;

    public string CheckoutBaseUrl { get; set; } =
        "https://fake-payment.invalid/checkout/";
}
