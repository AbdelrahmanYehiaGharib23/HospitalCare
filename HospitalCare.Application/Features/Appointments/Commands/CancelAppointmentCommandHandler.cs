using HospitalCare.Application.Common.Constants;
using HospitalCare.Application.Common.Exceptions;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Common.Models;
using HospitalCare.Application.Common.Services;
using HospitalCare.Application.Features.Appointments.DTOs;
using HospitalCare.Domain.Entities;
using HospitalCare.Domain.Enums;
using MediatR;

namespace HospitalCare.Application.Features.Appointments.Commands;

public sealed class CancelAppointmentCommandHandler
    : IRequestHandler<CancelAppointmentCommand, CancelledAppointmentDto>
{
    private const string Currency = "EGP";

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPaymentGateway _paymentGateway;
    private readonly DoctorDeactivationService _doctorDeactivationService;

    public CancelAppointmentCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IPaymentGateway paymentGateway,
        DoctorDeactivationService doctorDeactivationService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _paymentGateway = paymentGateway;
        _doctorDeactivationService = doctorDeactivationService;
    }

    public async Task<CancelledAppointmentDto> Handle(
        CancelAppointmentCommand request,
        CancellationToken cancellationToken)
    {
        var cancellation = await _unitOfWork.ExecuteSerializableTransactionAsync(
            token => CancelWithinTransactionAsync(request, token),
            cancellationToken);

        if (cancellation.RefundRequest is not null)
        {
            await _paymentGateway.RefundAsync(
                cancellation.RefundRequest,
                cancellationToken);

            var paymentTransaction = await _unitOfWork.PaymentTransactions
                .GetByIdAsync(
                    cancellation.PaymentTransactionId,
                    cancellationToken);
            if (paymentTransaction is null)
            {
                throw new NotFoundException(
                    "Payment transaction was not found.");
            }

            paymentTransaction.Status = PaymentStatus.Refunded;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var appointment = await _unitOfWork.Appointments.GetByIdAsync(
            request.AppointmentId,
            cancellationToken);
        var payment = await _unitOfWork.PaymentTransactions.GetByIdAsync(
            cancellation.PaymentTransactionId,
            cancellationToken);
        if (appointment is null || payment is null)
        {
            throw new NotFoundException("Appointment was not found.");
        }

        return CancelledAppointmentDto.FromEntities(appointment, payment);
    }

    private async Task<CancellationResult> CancelWithinTransactionAsync(
        CancelAppointmentCommand request,
        CancellationToken cancellationToken)
    {
        var appointment = await _unitOfWork.Appointments.GetByIdAsync(
            request.AppointmentId,
            cancellationToken);
        if (appointment is null)
        {
            throw new NotFoundException("Appointment was not found.");
        }

        EnsureCanCancel(appointment);

        if (!appointment.PaymentTransactionId.HasValue)
        {
            throw new BusinessRuleException(
                "The Appointment does not have a payment transaction.");
        }

        var paymentTransactions = await _unitOfWork.PaymentTransactions.FindAsync(
            payment => payment.Id == appointment.PaymentTransactionId.Value,
            withTracking: true,
            cancellationToken);
        var paymentTransaction = paymentTransactions.SingleOrDefault();
        if (paymentTransaction is null)
        {
            throw new NotFoundException("Payment transaction was not found.");
        }

        PaymentRefundRequest? refundRequest = null;
        if (appointment.Status == AppointmentStatus.Cancelled)
        {
            if (paymentTransaction.Status is not PaymentStatus.RefundPending and
                not PaymentStatus.Refunded)
            {
                throw new BusinessRuleException(
                    "The cancelled Appointment cannot be refunded from its current payment status.");
            }

            return new CancellationResult(
                paymentTransaction.Id,
                refundRequest);
        }

        if (paymentTransaction.Status != PaymentStatus.Succeeded)
        {
            throw new BusinessRuleException(
                "Only a successfully paid Appointment can be cancelled.");
        }

        try
        {
            appointment.Cancel();
        }
        catch (InvalidOperationException exception)
        {
            throw new BusinessRuleException(exception.Message);
        }

        paymentTransaction.Status = PaymentStatus.RefundPending;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _doctorDeactivationService.ApplyPendingDeactivationAsync(
            appointment.DoctorId,
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CancellationResult(
            paymentTransaction.Id,
            CreateRefundRequest(paymentTransaction));
    }

    private void EnsureCanCancel(Appointment appointment)
    {
        if (_currentUserService.IsInRole(Roles.Admin) ||
            _currentUserService.IsInRole(Roles.Receptionist))
        {
            return;
        }

        if (!_currentUserService.IsInRole(Roles.Patient) ||
            !int.TryParse(_currentUserService.PatientId, out var patientId))
        {
            throw new ForbiddenException(
                "The current user cannot cancel this Appointment.");
        }

        if (appointment.PatientId != patientId)
        {
            throw new ForbiddenException(
                "Patients can only cancel their own Appointments.");
        }
    }

    private static PaymentRefundRequest CreateRefundRequest(
        PaymentTransaction paymentTransaction)
    {
        if (string.IsNullOrWhiteSpace(paymentTransaction.GatewayReference))
        {
            throw new BusinessRuleException(
                "The payment transaction has no gateway reference.");
        }

        return new PaymentRefundRequest(
            paymentTransaction.Id,
            paymentTransaction.GatewayReference,
            paymentTransaction.Amount,
            Currency,
            paymentTransaction.ProviderTransactionId);
    }

    private sealed record CancellationResult(
        int PaymentTransactionId,
        PaymentRefundRequest? RefundRequest);
}
