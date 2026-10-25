using HospitalCare.Application.Common.Constants;
using HospitalCare.Application.Common.Exceptions;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Features.Appointments.DTOs;
using HospitalCare.Domain.Enums;
using MediatR;

namespace HospitalCare.Application.Features.Appointments.Commands;

public sealed class StartAppointmentCommandHandler
    : IRequestHandler<StartAppointmentCommand, AppointmentDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly TimeProvider _timeProvider;

    public StartAppointmentCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        TimeProvider timeProvider)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _timeProvider = timeProvider;
    }

    public async Task<AppointmentDto> Handle(
        StartAppointmentCommand request,
        CancellationToken cancellationToken)
    {
        var doctorId = GetCurrentDoctorId();
        return await _unitOfWork.ExecuteSerializableTransactionAsync(
            token => StartAsync(request.AppointmentId, doctorId, token),
            cancellationToken);
    }

    private async Task<AppointmentDto> StartAsync(
        int appointmentId,
        int doctorId,
        CancellationToken cancellationToken)
    {
        var appointment = await _unitOfWork.Appointments.GetByIdAsync(
            appointmentId,
            cancellationToken);
        if (appointment is null)
        {
            throw new NotFoundException("Appointment was not found.");
        }

        EnsureDoctorOwnsAppointment(appointment.DoctorId, doctorId);
        var currentAppointments = await _unitOfWork.Appointments.FindAsync(
            candidate =>
                candidate.DoctorId == doctorId &&
                candidate.AppointmentDate == appointment.AppointmentDate &&
                candidate.Status == AppointmentStatus.InProgress,
            withTracking: false,
            cancellationToken);
        if (currentAppointments.Any())
        {
            throw new ConflictException(
                "Another Appointment is already in progress for this queue.");
        }

        try
        {
            appointment.Start(_timeProvider.GetUtcNow().UtcDateTime);
        }
        catch (InvalidOperationException exception)
        {
            throw new BusinessRuleException(exception.Message);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return AppointmentDto.FromEntity(appointment);
    }

    private int GetCurrentDoctorId()
    {
        if (!_currentUserService.IsInRole(Roles.Doctor) ||
            !int.TryParse(_currentUserService.DoctorId, out var doctorId))
        {
            throw new ForbiddenException(
                "The current user cannot start Appointments.");
        }

        return doctorId;
    }

    private static void EnsureDoctorOwnsAppointment(
        int appointmentDoctorId,
        int currentDoctorId)
    {
        if (appointmentDoctorId != currentDoctorId)
        {
            throw new ForbiddenException(
                "Doctors can only start their own Appointments.");
        }
    }
}
