using HospitalCare.Application.Common.Models;

namespace HospitalCare.Application.Common.Interfaces;

public interface IPaymentGateway
{
    Task<PaymentCheckoutSession> CreateCheckoutAsync(
        PaymentCheckoutRequest request,
        CancellationToken cancellationToken = default);

    Task RefundAsync(
        PaymentRefundRequest request,
        CancellationToken cancellationToken = default);

    bool VerifyWebhook(string payload, string signature);

    PaymentWebhookEvent? ParseWebhook(string payload);
}
