using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HospitalCare.Application.Common.Models;
using HospitalCare.Infrastructure.Payments;
using Microsoft.Extensions.Options;

namespace HospitalCare.API.Tests.Payments;

public sealed class PaymobPaymentGatewayTests
{
    private const string HmacSecret = "paymob-test-hmac-secret";

    [Fact]
    public async Task CreateCheckoutAsync_ValidRequest_CreatesIntentionAndCheckoutUrl()
    {
        HttpRequestMessage? sentRequest = null;
        using var client = new HttpClient(new StubHttpMessageHandler(
            (request, _) =>
            {
                sentRequest = request;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent(
                        "{\"client_secret\":\"csk_test_abc\",\"intention_order_id\":654321}")
                });
            }));
        var gateway = CreateGateway(client);

        var checkout = await gateway.CreateCheckoutAsync(
            new PaymentCheckoutRequest(
                17,
                100.50m,
                "EGP",
                "hospitalcare-payment-17",
                "Mona Ali",
                "mona@example.com",
                "+201000000000"),
            TestContext.Current.CancellationToken);

        Assert.NotNull(sentRequest);
        Assert.Equal(HttpMethod.Post, sentRequest.Method);
        Assert.Equal(
            "https://accept.paymob.com/v1/intention/",
            sentRequest.RequestUri!.ToString());
        Assert.Equal("Token", sentRequest.Headers.Authorization!.Scheme);
        Assert.Equal("paymob-secret", sentRequest.Headers.Authorization.Parameter);
        using var body = JsonDocument.Parse(
            await sentRequest.Content!.ReadAsStringAsync(
                TestContext.Current.CancellationToken));
        var root = body.RootElement;
        Assert.Equal(10050, root.GetProperty("amount").GetInt32());
        Assert.Equal("EGP", root.GetProperty("currency").GetString());
        Assert.Equal(456789, root.GetProperty("payment_methods")[0].GetInt32());
        Assert.Equal(
            "hospitalcare-payment-17",
            root.GetProperty("special_reference").GetString());
        Assert.Equal(
            "https://hospital.example/api/payments/webhook",
            root.GetProperty("notification_url").GetString());
        Assert.Equal(
            "https://hospital.example/payment/complete",
            root.GetProperty("redirection_url").GetString());
        Assert.Equal("Mona", root.GetProperty("billing_data").GetProperty("first_name").GetString());
        Assert.Equal("Ali", root.GetProperty("billing_data").GetProperty("last_name").GetString());
        Assert.Equal(
            "https://accept.paymob.com/unifiedcheckout/?publicKey=pk_test_abc&clientSecret=csk_test_abc",
            checkout.CheckoutUrl);
        Assert.Equal("654321", checkout.GatewayReference);
    }

    [Fact]
    public async Task CreateCheckoutAsync_AmountHasMoreThanTwoDecimalPlaces_Throws()
    {
        using var client = new HttpClient(new StubHttpMessageHandler(
            (_, _) => throw new InvalidOperationException()));
        var gateway = CreateGateway(client);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            gateway.CreateCheckoutAsync(
                new PaymentCheckoutRequest(
                    17,
                    1.001m,
                    "EGP",
                    "hospitalcare-payment-17",
                    "Mona Ali",
                    "mona@example.com",
                    "+201000000000"),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateCheckoutAsync_PatientHasOneName_OmitsLastName()
    {
        HttpRequestMessage? sentRequest = null;
        using var client = new HttpClient(new StubHttpMessageHandler(
            (request, _) =>
            {
                sentRequest = request;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent(
                        "{\"client_secret\":\"csk_test_abc\",\"intention_order_id\":654321}")
                });
            }));
        var gateway = CreateGateway(client);

        await gateway.CreateCheckoutAsync(
            new PaymentCheckoutRequest(
                17,
                1m,
                "EGP",
                "hospitalcare-payment-17",
                "Mona",
                "mona@example.com",
                "+201000000000"),
            TestContext.Current.CancellationToken);

        Assert.NotNull(sentRequest);
        using var body = JsonDocument.Parse(
            await sentRequest.Content!.ReadAsStringAsync(
                TestContext.Current.CancellationToken));
        Assert.Equal(
            "Mona",
            body.RootElement.GetProperty("billing_data").GetProperty("first_name").GetString());
        Assert.False(
            body.RootElement.GetProperty("billing_data").TryGetProperty(
                "last_name",
                out _));
    }

    [Fact]
    public async Task RefundAsync_ValidRequest_UsesProviderTransactionAndAmountInCents()
    {
        HttpRequestMessage? sentRequest = null;
        using var client = new HttpClient(new StubHttpMessageHandler(
            (request, _) =>
            {
                sentRequest = request;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            }));
        var gateway = CreateGateway(client);

        await gateway.RefundAsync(
            new PaymentRefundRequest(
                17,
                "hospitalcare-payment-17",
                123.45m,
                "EGP",
                987654),
            TestContext.Current.CancellationToken);

        Assert.NotNull(sentRequest);
        Assert.Equal(
            "https://accept.paymob.com/api/acceptance/void_refund/refund",
            sentRequest.RequestUri!.ToString());
        using var body = JsonDocument.Parse(
            await sentRequest.Content!.ReadAsStringAsync(
                TestContext.Current.CancellationToken));
        Assert.Equal(987654, body.RootElement.GetProperty("transaction_id").GetInt64());
        Assert.Equal(12345, body.RootElement.GetProperty("amount_cents").GetInt32());
    }

    [Fact]
    public void VerifyWebhook_OfficialHmacFieldOrder_ReturnsTrue()
    {
        using var client = new HttpClient();
        var gateway = CreateGateway(client);
        const string payload = """
            {"obj":{"amount_cents":100,"created_at":"2020-03-25T18:39:44.719228","currency":"EGP","error_occured":false,"has_parent_transaction":false,"id":2556706,"integration_id":6741,"is_3d_secure":true,"is_auth":false,"is_capture":false,"is_refunded":false,"is_standalone_payment":true,"is_voided":false,"order":{"id":4778239},"owner":4705,"pending":false,"source_data":{"pan":"2346","sub_type":"MasterCard","type":"card"},"success":true}}
            """;
        const string signedData = "1002020-03-25T18:39:44.719228EGPfalsefalse25567066741truefalsefalsefalsetruefalse47782394705false2346MasterCardcardtrue";
        var signature = Sign(signedData);

        Assert.True(gateway.VerifyWebhook(payload, signature));
        Assert.False(gateway.VerifyWebhook(payload, Sign(signedData.Replace("true", "false"))));
    }

    [Fact]
    public void VerifyWebhook_NullHmacField_ReturnsFalse()
    {
        using var client = new HttpClient();
        var gateway = CreateGateway(client);
        var payload = CreateCallbackPayload(pan: null);

        Assert.False(gateway.VerifyWebhook(payload, new string('0', 128)));
    }

    [Fact]
    public void ParseWebhook_SuccessfulTransaction_ReturnsSignedOrderAndProviderReferences()
    {
        using var client = new HttpClient();
        var gateway = CreateGateway(client);

        var webhookEvent = gateway.ParseWebhook(CreateCallbackPayload())!;

        Assert.Equal("654321", webhookEvent.GatewayReference);
        Assert.Equal(PaymentWebhookStatus.Succeeded, webhookEvent.Status);
        Assert.Equal(100.50m, webhookEvent.Amount);
        Assert.Equal("EGP", webhookEvent.Currency);
        Assert.Equal(123456, webhookEvent.ProviderTransactionId);
        Assert.False(webhookEvent.IsRefunded);
    }

    [Fact]
    public void ParseWebhook_MerchantReferenceIsChanged_UsesSignedOrderId()
    {
        using var client = new HttpClient();
        var gateway = CreateGateway(client);
        var payload = CreateCallbackPayload()
            .Replace(
                "hospitalcare-payment-17",
                "hospitalcare-payment-999",
                StringComparison.Ordinal);
        const string signedData = "100502026-10-25T08:00:00EGPfalsefalse123456456789truefalsefalsefalsetruefalse6543214705false2346MasterCardcardtrue";

        Assert.True(gateway.VerifyWebhook(payload, Sign(signedData)));
        Assert.Equal("654321", gateway.ParseWebhook(payload)!.GatewayReference);
    }

    [Fact]
    public void ParseWebhook_RefundCallback_DoesNotTrustUnsignedRefundAmount()
    {
        using var client = new HttpClient();
        var gateway = CreateGateway(client);
        var payload = CreateCallbackPayload(
            isRefunded: true,
            refundedAmountCents: 10050);

        var webhookEvent = gateway.ParseWebhook(payload)!;

        Assert.Equal(PaymentWebhookStatus.Pending, webhookEvent.Status);
        Assert.True(webhookEvent.IsRefunded);
        Assert.Equal("654321", webhookEvent.GatewayReference);
    }

    private static PaymobPaymentGateway CreateGateway(HttpClient client)
    {
        return new PaymobPaymentGateway(
            client,
            Options.Create(new PaymobPaymentGatewayOptions
            {
                BaseUrl = "https://accept.paymob.com",
                SecretKey = "paymob-secret",
                PublicKey = "pk_test_abc",
                HmacSecret = HmacSecret,
                IntegrationId = 456789,
                Currency = "EGP",
                NotificationUrl = "https://hospital.example/api/payments/webhook",
                RedirectUrl = "https://hospital.example/payment/complete"
            }));
    }

    private static string CreateCallbackPayload(
        string? pan = "2346",
        bool isRefunded = false,
        long refundedAmountCents = 0)
    {
        var transaction = new Dictionary<string, object?>
        {
            ["amount_cents"] = 10050,
            ["created_at"] = "2026-10-25T08:00:00",
            ["currency"] = "EGP",
            ["error_occured"] = false,
            ["has_parent_transaction"] = false,
            ["id"] = 123456,
            ["integration_id"] = 456789,
            ["is_3d_secure"] = true,
            ["is_auth"] = false,
            ["is_capture"] = false,
            ["is_refunded"] = isRefunded,
            ["is_standalone_payment"] = true,
            ["is_voided"] = false,
            ["order"] = new Dictionary<string, object>
            {
                ["id"] = 654321,
                ["merchant_order_id"] = "hospitalcare-payment-17"
            },
            ["owner"] = 4705,
            ["pending"] = false,
            ["source_data"] = new Dictionary<string, object?>
            {
                ["pan"] = pan,
                ["sub_type"] = "MasterCard",
                ["type"] = "card"
            },
            ["success"] = true,
            ["refunded_amount_cents"] = refundedAmountCents
        };

        return JsonSerializer.Serialize(new { obj = transaction });
    }

    private static string Sign(string value)
    {
        return Convert.ToHexString(HMACSHA512.HashData(
                Encoding.UTF8.GetBytes(HmacSecret),
                Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
