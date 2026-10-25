using HospitalCare.Application.Common.Exceptions;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Common.Models;
using HospitalCare.Application.Features.Payments.DTOs;
using HospitalCare.Domain.Entities;
using HospitalCare.Domain.Enums;
using HospitalCare.Domain.Services;
using MediatR;

namespace HospitalCare.Application.Features.Appointments.Commands;

public sealed class ConfirmAppointmentPaymentCommandHandler
    : IRequestHandler<
        ConfirmAppointmentPaymentCommand,
        PaymentWebhookResultDto>
{
    private const string Currency = "EGP";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentGateway _paymentGateway;
    private readonly DoctorCapacityCalculator _capacityCalculator;
    private readonly TimeProvider _timeProvider;
    private readonly TimeZoneInfo _hospitalTimeZone;

    public ConfirmAppointmentPaymentCommandHandler(
        IUnitOfWork unitOfWork,
        IPaymentGateway paymentGateway,
        DoctorCapacityCalculator capacityCalculator,
        TimeProvider timeProvider,
        TimeZoneInfo hospitalTimeZone)
    {
        _unitOfWork = unitOfWork;
        _paymentGateway = paymentGateway;
        _capacityCalculator = capacityCalculator;
        _timeProvider = timeProvider;
        _hospitalTimeZone = hospitalTimeZone;
    }

    public async Task<PaymentWebhookResultDto> Handle(
        ConfirmAppointmentPaymentCommand request,
        CancellationToken cancellationToken)
    {
        if (!_paymentGateway.VerifyWebhook(request.Payload, request.Signature))
        {
            throw new BusinessRuleException(
                "The payment webhook signature is invalid.");
        }

        var webhookEvent = _paymentGateway.ParseWebhook(request.Payload);
        if (webhookEvent is null ||
            !Enum.IsDefined(webhookEvent.Status) ||
            webhookEvent.Amount <= 0)
        {
            throw new BusinessRuleException(
                "The payment webhook payload is invalid.");
        }

        var result = await _unitOfWork.ExecuteSerializableTransactionAsync(
            token => ConfirmPaymentAsync(webhookEvent, token),
            cancellationToken);

        if (result.RefundRequest is null)
        {
            return result.Response;
        }

        await _paymentGateway.RefundAsync(
            result.RefundRequest,
            cancellationToken);

        var paymentTransaction = await _unitOfWork.PaymentTransactions
            .GetByIdAsync(result.Response.PaymentTransactionId, cancellationToken);
        if (paymentTransaction is null)
        {
            throw new NotFoundException("Payment transaction was not found.");
        }

        paymentTransaction.Status = PaymentStatus.Refunded;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return result.Response with { Status = PaymentStatus.Refunded };
    }

    private async Task<ConfirmationResult> ConfirmPaymentAsync(
        PaymentWebhookEvent webhookEvent,
        CancellationToken cancellationToken)
    {
        var transactions = await _unitOfWork.PaymentTransactions.FindAsync(
            transaction =>
                transaction.GatewayReference == webhookEvent.GatewayReference,
            withTracking: true,
            cancellationToken);
        var transaction = transactions.SingleOrDefault();
        if (transaction is null)
        {
            throw new NotFoundException("Payment transaction was not found.");
        }

        if (transaction.Amount != webhookEvent.Amount ||
            !string.Equals(
                webhookEvent.Currency,
                Currency,
                StringComparison.Ordinal))
        {
            throw new BusinessRuleException(
                "The payment amount or currency does not match the transaction.");
        }

        var appointments = await _unitOfWork.Appointments.FindAsync(
            appointment =>
                appointment.PaymentTransactionId == transaction.Id,
            withTracking: false,
            cancellationToken);
        var appointment = appointments.SingleOrDefault();

        if (transaction.Status is PaymentStatus.Failed or PaymentStatus.Refunded)
        {
            return ConfirmationResult.Completed(
                CreateResponse(transaction, appointment));
        }

        if (appointment is not null &&
            (appointment.Status != AppointmentStatus.Cancelled ||
                transaction.Status != PaymentStatus.RefundPending))
        {
            return ConfirmationResult.Completed(
                CreateResponse(transaction, appointment));
        }

        if (transaction.Status is PaymentStatus.Succeeded or
            PaymentStatus.RefundPending)
        {
            return ConfirmationResult.RequiresRefund(
                CreateResponse(transaction, appointment),
                CreateRefundRequest(transaction));
        }

        if (webhookEvent.Status == PaymentWebhookStatus.Failed)
        {
            transaction.Status = PaymentStatus.Failed;
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return ConfirmationResult.Completed(
                CreateResponse(transaction, null));
        }

        if (!await HasCapacityAsync(transaction, cancellationToken))
        {
            transaction.Status = PaymentStatus.RefundPending;
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return ConfirmationResult.RequiresRefund(
                CreateResponse(transaction, null),
                CreateRefundRequest(transaction));
        }

        transaction.Status = PaymentStatus.Succeeded;

        var queuePosition = await GetNextQueuePositionAsync(
            transaction.DoctorId,
            transaction.RequestedDate,
            cancellationToken);
        var newAppointment = new Appointment
        {
            DoctorId = transaction.DoctorId,
            PatientId = transaction.PatientId,
            AppointmentTypeId = transaction.AppointmentTypeId,
            PaymentTransactionId = transaction.Id,
            AppointmentDate = transaction.RequestedDate,
            QueuePosition = queuePosition,
            Status = AppointmentStatus.Queued
        };

        _unitOfWork.Appointments.Add(newAppointment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ConfirmationResult.Completed(
            new PaymentWebhookResultDto(
                transaction.Id,
                transaction.Status,
                newAppointment.Id,
                newAppointment.QueuePosition));
    }

    private async Task<bool> HasCapacityAsync(
        PaymentTransaction transaction,
        CancellationToken cancellationToken)
    {
        var doctors = await _unitOfWork.Doctors.FindAsync(
            doctor => doctor.Id == transaction.DoctorId,
            withTracking: false,
            cancellationToken);
        var doctor = doctors.SingleOrDefault();
        if (doctor is null || !doctor.IsActive || doctor.PendingDeactivation)
        {
            return false;
        }

        var appointmentTypes = await _unitOfWork.AppointmentTypes.FindAsync(
            appointmentType =>
                appointmentType.Id == transaction.AppointmentTypeId,
            withTracking: false,
            cancellationToken);
        var appointmentType = appointmentTypes.SingleOrDefault();
        if (appointmentType is null)
        {
            return false;
        }

        var schedules = (await _unitOfWork.DoctorSchedules.FindAsync(
            schedule =>
                schedule.DoctorId == transaction.DoctorId &&
                schedule.DayOfWeek == transaction.RequestedDate.DayOfWeek,
            withTracking: false,
            cancellationToken)).ToArray();
        if (schedules.Length == 0)
        {
            return false;
        }

        var appointments = (await _unitOfWork.Appointments.FindAsync(
            appointment =>
                appointment.DoctorId == transaction.DoctorId &&
                appointment.AppointmentDate == transaction.RequestedDate &&
                (appointment.Status == AppointmentStatus.Queued ||
                    appointment.Status == AppointmentStatus.InProgress),
            withTracking: false,
            cancellationToken)).ToArray();
        var appointmentTypeIds = appointments
            .Select(appointment => appointment.AppointmentTypeId)
            .Append(transaction.AppointmentTypeId)
            .Distinct()
            .ToArray();
        var durations = (await _unitOfWork.AppointmentTypes.FindAsync(
                type => appointmentTypeIds.Contains(type.Id),
                withTracking: false,
                cancellationToken))
            .ToDictionary(type => type.Id, type => type.DurationMinutes);
        var capacityAppointments = appointments
            .Select(appointment => new DoctorCapacityAppointment(
                appointment.AppointmentDate,
                appointment.Status,
                durations[appointment.AppointmentTypeId]))
            .ToArray();
        var hospitalLocalNow = TimeZoneInfo.ConvertTime(
            _timeProvider.GetUtcNow(),
            _hospitalTimeZone);
        var capacity = _capacityCalculator.Calculate(
            transaction.RequestedDate,
            schedules,
            capacityAppointments,
            appointmentType.DurationMinutes,
            hospitalLocalNow);

        return capacity.CanFit;
    }

    private async Task<int> GetNextQueuePositionAsync(
        int doctorId,
        DateOnly appointmentDate,
        CancellationToken cancellationToken)
    {
        var appointments = await _unitOfWork.Appointments.FindAsync(
            appointment =>
                appointment.DoctorId == doctorId &&
                appointment.AppointmentDate == appointmentDate,
            withTracking: false,
            cancellationToken);

        return appointments
            .Select(appointment => appointment.QueuePosition)
            .DefaultIfEmpty()
            .Max() + 1;
    }

    private static PaymentWebhookResultDto CreateResponse(
        PaymentTransaction transaction,
        Appointment? appointment)
    {
        return new PaymentWebhookResultDto(
            transaction.Id,
            transaction.Status,
            appointment?.Id,
            appointment?.QueuePosition);
    }

    private static PaymentRefundRequest CreateRefundRequest(
        PaymentTransaction transaction)
    {
        return new PaymentRefundRequest(
            transaction.Id,
            transaction.GatewayReference!,
            transaction.Amount,
            Currency);
    }

    private sealed record ConfirmationResult(
        PaymentWebhookResultDto Response,
        PaymentRefundRequest? RefundRequest)
    {
        public static ConfirmationResult Completed(
            PaymentWebhookResultDto response) => new(response, null);

        public static ConfirmationResult RequiresRefund(
            PaymentWebhookResultDto response,
            PaymentRefundRequest refundRequest) =>
            new(response, refundRequest);
    }
}
