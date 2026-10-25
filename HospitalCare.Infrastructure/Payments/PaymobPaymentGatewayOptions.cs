namespace HospitalCare.Infrastructure.Payments;

public sealed class PaymobPaymentGatewayOptions
{
    public const string SectionName = "Paymob";

    public string BaseUrl { get; set; } = "https://accept.paymob.com";

    public string SecretKey { get; set; } = string.Empty;

    public string PublicKey { get; set; } = string.Empty;

    public string HmacSecret { get; set; } = string.Empty;

    public int IntegrationId { get; set; }

    public string Currency { get; set; } = "EGP";

    public string NotificationUrl { get; set; } = string.Empty;

    public string? RedirectUrl { get; set; }
}
