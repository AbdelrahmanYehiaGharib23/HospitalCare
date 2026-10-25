using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Common.Services;
using HospitalCare.Domain.Entities;
using HospitalCare.Domain.Enums;
using MediatR;

namespace HospitalCare.Application.Features.Appointments.Commands;

public sealed class ProcessNoShowSwapCommandHandler
    : IRequestHandler<ProcessNoShowSwapCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly DoctorDeactivationService _doctorDeactivationService;

    public ProcessNoShowSwapCommandHandler(
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        DoctorDeactivationService doctorDeactivationService)
    {
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _doctorDeactivationService = doctorDeactivationService;
    }

    public Task<bool> Handle(
        ProcessNoShowSwapCommand request,
        CancellationToken cancellationToken)
    {
        return _unitOfWork.ExecuteSerializableTransactionAsync(
            token => ProcessWithinTransactionAsync(request, token),
            cancellationToken);
    }

    private async Task<bool> ProcessWithinTransactionAsync(
        ProcessNoShowSwapCommand request,
        CancellationToken cancellationToken)
    {
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var appointment = await _unitOfWork.Appointments.GetByIdAsync(
            request.AppointmentId,
            cancellationToken);
        if (appointment is null ||
            !appointment.IsNoShowDue(utcNow))
        {
            return false;
        }

        if (appointment.SkipCount > 0)
        {
            appointment.PenalizeNoShow(utcNow);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _doctorDeactivationService.ApplyPendingDeactivationAsync(
                appointment.DoctorId,
                cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }

        var nextAppointment = (await _unitOfWork.Appointments.FindAsync(
                candidate =>
                    candidate.DoctorId == appointment.DoctorId &&
                    candidate.AppointmentDate == appointment.AppointmentDate &&
                    candidate.Status == AppointmentStatus.Queued &&
                    candidate.QueuePosition > appointment.QueuePosition,
                withTracking: true,
                cancellationToken))
            .MinBy(candidate => candidate.QueuePosition);
        if (nextAppointment is null)
        {
            return false;
        }

        var missedQueuePosition = appointment.QueuePosition;
        var nextQueuePosition = nextAppointment.QueuePosition;
        var queueAppointments = await _unitOfWork.Appointments.FindAsync(
            candidate =>
                candidate.DoctorId == appointment.DoctorId &&
                candidate.AppointmentDate == appointment.AppointmentDate,
            withTracking: false,
            cancellationToken);
        var temporaryQueuePosition = queueAppointments
            .Select(candidate => candidate.QueuePosition)
            .Max() + 1;

        // The temporary position keeps the unique queue index valid during the swap.
        appointment.MoveToQueuePosition(temporaryQueuePosition);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        nextAppointment.MoveToQueuePosition(missedQueuePosition);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        appointment.RecordFirstNoShow(nextQueuePosition, utcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
