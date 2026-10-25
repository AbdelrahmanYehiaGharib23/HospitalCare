using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Common.Models;
using Microsoft.Extensions.Options;

namespace HospitalCare.Infrastructure.Payments;

public sealed class PaymobPaymentGateway : IPaymentGateway
{
    private const string Currency = "EGP";
    private const string CreateIntentionPath = "v1/intention/";
    private const string RefundPath = "api/acceptance/void_refund/refund";
    private const string CheckoutPath = "unifiedcheckout/";

    private static readonly string[] HmacFieldPaths =
    [
        "amount_cents",
        "created_at",
        "currency",
        "error_occured",
        "has_parent_transaction",
        "id",
        "integration_id",
        "is_3d_secure",
        "is_auth",
        "is_capture",
        "is_refunded",
        "is_standalone_payment",
        "is_voided",
        "order.id",
        "owner",
        "pending",
        "source_data.pan",
        "source_data.sub_type",
        "source_data.type",
        "success"
    ];

    private readonly HttpClient _httpClient;
    private readonly PaymobPaymentGatewayOptions _options;
    private readonly Uri _baseUri;
    private readonly byte[] _hmacKey;

    public PaymobPaymentGateway(
        HttpClient httpClient,
        IOptions<PaymobPaymentGatewayOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _baseUri = ValidateBaseUrl(_options.BaseUrl);
        ValidateOptions(_options);
        _hmacKey = Encoding.UTF8.GetBytes(_options.HmacSecret);

        _httpClient.BaseAddress = _baseUri;
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Token", _options.SecretKey);
    }

    public async Task<PaymentCheckoutSession> CreateCheckoutAsync(
        PaymentCheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        var amountCents = ToAmountCents(request.Amount);
        if (!string.Equals(
                request.Currency,
                _options.Currency,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Paymob is configured for EGP payments only.");
        }

        if (string.IsNullOrWhiteSpace(request.PatientPhone))
        {
            throw new InvalidOperationException(
                "A patient phone number is required for Paymob checkout.");
        }

        if (string.IsNullOrWhiteSpace(request.MerchantReference))
        {
            throw new InvalidOperationException(
                "A merchant reference is required for Paymob checkout.");
        }

        var (firstName, lastName) = SplitName(request.PatientName);
        var intention = new CreateIntentionRequest(
            amountCents,
            request.Currency,
            [_options.IntegrationId],
            new BillingData(
                firstName,
                lastName,
                request.PatientEmail,
                request.PatientPhone),
            request.MerchantReference,
            _options.NotificationUrl,
            string.IsNullOrWhiteSpace(_options.RedirectUrl)
                ? null
                : _options.RedirectUrl);

        using var response = await _httpClient.PostAsJsonAsync(
            CreateIntentionPath,
            intention,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync(
            cancellationToken);
        using var responseDocument = await JsonDocument.ParseAsync(
            responseStream,
            cancellationToken: cancellationToken);
        if (!responseDocument.RootElement.TryGetProperty(
                "client_secret",
                out var clientSecretElement) ||
            clientSecretElement.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(clientSecretElement.GetString()) ||
            !responseDocument.RootElement.TryGetProperty(
                "intention_order_id",
                out var orderIdElement) ||
            !orderIdElement.TryGetInt64(out var orderId) ||
            orderId <= 0)
        {
            throw new InvalidOperationException(
                "Paymob did not return a checkout client secret and order ID.");
        }

        var clientSecret = clientSecretElement.GetString()!;
        var checkoutUri = new UriBuilder(_baseUri)
        {
            Path = CheckoutPath,
            Query = $"publicKey={Uri.EscapeDataString(_options.PublicKey)}&clientSecret={Uri.EscapeDataString(clientSecret)}"
        }.Uri;

        return new PaymentCheckoutSession(
            orderId.ToString(CultureInfo.InvariantCulture),
            checkoutUri.ToString());
    }

    public async Task RefundAsync(
        PaymentRefundRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(
                request.Currency,
                _options.Currency,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Paymob is configured for EGP payments only.");
        }

        if (!request.ProviderTransactionId.HasValue)
        {
            throw new InvalidOperationException(
                "The Paymob transaction ID is required to refund a payment.");
        }

        var refund = new RefundRequest(
            request.ProviderTransactionId.Value,
            ToAmountCents(request.Amount));
        using var response = await _httpClient.PostAsJsonAsync(
            RefundPath,
            refund,
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public bool VerifyWebhook(string payload, string signature)
    {
        if (string.IsNullOrWhiteSpace(signature) || signature.Length != 128)
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            if (!TryGetTransaction(document.RootElement, out var transaction))
            {
                return false;
            }

            var values = new string[HmacFieldPaths.Length];
            for (var index = 0; index < HmacFieldPaths.Length; index++)
            {
                if (!TryGetHmacValue(
                        transaction,
                        HmacFieldPaths[index],
                        out values[index]))
                {
                    return false;
                }
            }

            // Paymob signs these fields in this order with no separators.
            var signedData = string.Concat(values);
            var expectedSignature = HMACSHA512.HashData(
                _hmacKey,
                Encoding.UTF8.GetBytes(signedData));
            var suppliedSignature = Convert.FromHexString(signature);

            return CryptographicOperations.FixedTimeEquals(
                expectedSignature,
                suppliedSignature);
        }
        catch (JsonException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public PaymentWebhookEvent? ParseWebhook(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            if (!TryGetTransaction(document.RootElement, out var transaction) ||
                !TryGetProperty(transaction, "amount_cents", out var amountElement) ||
                !amountElement.TryGetInt64(out var amountCents) ||
                amountCents <= 0 ||
                !TryGetProperty(transaction, "id", out var idElement) ||
                !idElement.TryGetInt64(out var providerTransactionId) ||
                !TryGetProperty(transaction, "currency", out var currencyElement) ||
                currencyElement.ValueKind != JsonValueKind.String ||
                !TryGetProperty(transaction, "pending", out var pendingElement) ||
                pendingElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                !TryGetProperty(transaction, "success", out var successElement) ||
                successElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                !TryGetProperty(transaction, "is_refunded", out var refundedElement) ||
                refundedElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                !TryGetProperty(transaction, "order", out var orderElement) ||
                !TryGetProperty(orderElement, "id", out var orderIdElement) ||
                !orderIdElement.TryGetInt64(out var orderId) ||
                orderId <= 0)
            {
                return null;
            }

            var currency = currencyElement.GetString();
            if (string.IsNullOrWhiteSpace(currency))
            {
                return null;
            }

            var isRefunded = refundedElement.GetBoolean();
            var status = pendingElement.GetBoolean() || isRefunded
                ? PaymentWebhookStatus.Pending
                : successElement.GetBoolean()
                    ? PaymentWebhookStatus.Succeeded
                    : PaymentWebhookStatus.Failed;

            return new PaymentWebhookEvent(
                orderId.ToString(CultureInfo.InvariantCulture),
                status,
                amountCents / 100m,
                currency,
                providerTransactionId,
                isRefunded);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Uri ValidateBaseUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            uri.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException(
                "Paymob:BaseUrl must be an HTTPS origin.");
        }

        return uri;
    }

    private static void ValidateOptions(PaymobPaymentGatewayOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.SecretKey) ||
            string.IsNullOrWhiteSpace(options.PublicKey) ||
            string.IsNullOrWhiteSpace(options.HmacSecret) ||
            options.IntegrationId <= 0 ||
            !string.Equals(options.Currency, Currency, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Paymob keys, integration ID, and EGP currency must be configured.");
        }

        if (!Uri.TryCreate(
                options.NotificationUrl,
                UriKind.Absolute,
                out var notificationUri) ||
            notificationUri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(notificationUri.UserInfo) ||
            !string.IsNullOrEmpty(notificationUri.Fragment))
        {
            throw new InvalidOperationException(
                "Paymob:NotificationUrl must be an HTTPS URL.");
        }
    }

    private static int ToAmountCents(decimal amount)
    {
        var cents = amount * 100m;
        if (cents <= 0 ||
            cents != decimal.Truncate(cents) ||
            cents > int.MaxValue)
        {
            throw new InvalidOperationException(
                "Paymob amounts must be positive EGP values with at most two decimal places.");
        }

        return decimal.ToInt32(cents);
    }

    private static (string FirstName, string LastName) SplitName(string name)
    {
        var parts = name.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return (string.Empty, string.Empty);
        }

        return (
            parts[0],
            parts.Length > 1 ? string.Join(' ', parts.Skip(1)) : string.Empty);
    }

    private static bool TryGetTransaction(
        JsonElement root,
        out JsonElement transaction)
    {
        transaction = default;
        return TryGetProperty(root, "obj", out transaction) &&
            transaction.ValueKind == JsonValueKind.Object;
    }

    private static bool TryGetHmacValue(
        JsonElement transaction,
        string path,
        out string value)
    {
        var current = transaction;
        foreach (var segment in path.Split('.'))
        {
            if (!TryGetProperty(current, segment, out current))
            {
                value = string.Empty;
                return false;
            }
        }

        switch (current.ValueKind)
        {
            case JsonValueKind.String:
                value = current.GetString()!;
                return true;
            case JsonValueKind.Number:
                value = current.GetRawText();
                return true;
            case JsonValueKind.True:
                value = bool.TrueString.ToLowerInvariant();
                return true;
            case JsonValueKind.False:
                value = bool.FalseString.ToLowerInvariant();
                return true;
            default:
                value = string.Empty;
                return false;
        }
    }

    private static bool TryGetProperty(
        JsonElement element,
        string propertyName,
        out JsonElement property)
    {
        property = default;
        return element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out property);
    }

    private sealed record CreateIntentionRequest(
        [property: JsonPropertyName("amount")] int Amount,
        [property: JsonPropertyName("currency")] string Currency,
        [property: JsonPropertyName("payment_methods")] int[] PaymentMethods,
        [property: JsonPropertyName("billing_data")] BillingData BillingData,
        [property: JsonPropertyName("special_reference")] string SpecialReference,
        [property: JsonPropertyName("notification_url")] string NotificationUrl,
        [property: JsonPropertyName("redirection_url")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? RedirectionUrl);

    private sealed record BillingData(
        [property: JsonPropertyName("first_name")] string FirstName,
        [property: JsonPropertyName("last_name")] string LastName,
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("phone_number")] string PhoneNumber);

    private sealed record RefundRequest(
        [property: JsonPropertyName("transaction_id")] long TransactionId,
        [property: JsonPropertyName("amount_cents")] int AmountCents);
}
