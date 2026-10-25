using HospitalCare.Application.Features.Payments.DTOs;
using MediatR;

namespace HospitalCare.Application.Features.Appointments.Commands;

public sealed record ConfirmAppointmentPaymentCommand(
    string Payload,
    string Signature)
    : IRequest<PaymentWebhookResultDto>;
