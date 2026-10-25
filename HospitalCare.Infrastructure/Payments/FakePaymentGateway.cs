using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Common.Models;
using Microsoft.Extensions.Options;

namespace HospitalCare.Infrastructure.Payments;

public sealed class FakePaymentGateway : IPaymentGateway
{
    private readonly FakePaymentGatewayOptions _options;

    public FakePaymentGateway(IOptions<FakePaymentGatewayOptions> options)
    {
        _options = options.Value;

        if (Encoding.UTF8.GetByteCount(_options.WebhookSecret) < 32)
        {
            throw new InvalidOperationException(
                "PaymentGateway:Fake:WebhookSecret must contain at least 32 UTF-8 bytes.");
        }

        if (!Uri.TryCreate(
                _options.CheckoutBaseUrl,
                UriKind.Absolute,
                out var checkoutBaseUri) ||
            checkoutBaseUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                "PaymentGateway:Fake:CheckoutBaseUrl must be an HTTPS URL.");
        }
    }

    public Task<PaymentCheckoutSession> CreateCheckoutAsync(
        PaymentCheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var gatewayReference = $"fake-{request.PaymentTransactionId}";
        var checkoutUrl = new Uri(
            new Uri(_options.CheckoutBaseUrl, UriKind.Absolute),
            Uri.EscapeDataString(gatewayReference));

        return Task.FromResult(
            new PaymentCheckoutSession(
                gatewayReference,
                checkoutUrl.ToString()));
    }

    public Task RefundAsync(
        PaymentRefundRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public bool VerifyWebhook(string payload, string signature)
    {
        if (string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        byte[] suppliedSignature;
        try
        {
            suppliedSignature = Convert.FromHexString(signature);
        }
        catch (FormatException)
        {
            return false;
        }

        var key = Encoding.UTF8.GetBytes(_options.WebhookSecret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var expectedSignature = HMACSHA256.HashData(key, payloadBytes);

        return CryptographicOperations.FixedTimeEquals(
            expectedSignature,
            suppliedSignature);
    }

    public PaymentWebhookEvent? ParseWebhook(string payload)
    {
        try
        {
            var webhookEvent = JsonSerializer.Deserialize<PaymentWebhookEvent>(
                payload,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    Converters = { new JsonStringEnumConverter() }
                });

            if (string.IsNullOrWhiteSpace(webhookEvent?.GatewayReference) ||
                webhookEvent.Amount < 0 ||
                string.IsNullOrWhiteSpace(webhookEvent.Currency))
            {
                return null;
            }

            return webhookEvent;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
