using HospitalCare.Application.Common.Constants;
using HospitalCare.Application.Common.Exceptions;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Common.Services;
using HospitalCare.Application.Features.Appointments.DTOs;
using MediatR;

namespace HospitalCare.Application.Features.Appointments.Commands;

public sealed class CompleteAppointmentCommandHandler
    : IRequestHandler<CompleteAppointmentCommand, AppointmentDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly TimeProvider _timeProvider;
    private readonly DoctorDeactivationService _doctorDeactivationService;

    public CompleteAppointmentCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        TimeProvider timeProvider,
        DoctorDeactivationService doctorDeactivationService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _timeProvider = timeProvider;
        _doctorDeactivationService = doctorDeactivationService;
    }

    public async Task<AppointmentDto> Handle(
        CompleteAppointmentCommand request,
        CancellationToken cancellationToken)
    {
        var doctorId = GetCurrentDoctorId();
        return await _unitOfWork.ExecuteSerializableTransactionAsync(
            token => CompleteAsync(request, doctorId, token),
            cancellationToken);
    }

    private async Task<AppointmentDto> CompleteAsync(
        CompleteAppointmentCommand request,
        int doctorId,
        CancellationToken cancellationToken)
    {
        var appointment = await _unitOfWork.Appointments.GetByIdAsync(
            request.AppointmentId,
            cancellationToken);
        if (appointment is null)
        {
            throw new NotFoundException("Appointment was not found.");
        }

        if (appointment.DoctorId != doctorId)
        {
            throw new ForbiddenException(
                "Doctors can only complete their own Appointments.");
        }

        try
        {
            appointment.Complete(
                _timeProvider.GetUtcNow().UtcDateTime,
                request.Notes);
        }
        catch (InvalidOperationException exception)
        {
            throw new BusinessRuleException(exception.Message);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _doctorDeactivationService.ApplyPendingDeactivationAsync(
            appointment.DoctorId,
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return AppointmentDto.FromEntity(appointment);
    }

    private int GetCurrentDoctorId()
    {
        if (!_currentUserService.IsInRole(Roles.Doctor) ||
            !int.TryParse(_currentUserService.DoctorId, out var doctorId))
        {
            throw new ForbiddenException(
                "The current user cannot complete Appointments.");
        }

        return doctorId;
    }
}
