using HospitalCare.Application.Common.Constants;
using HospitalCare.Application.Common.Exceptions;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Features.Appointments.DTOs;
using HospitalCare.Domain.Enums;
using MediatR;

namespace HospitalCare.Application.Features.Appointments.Commands;

public sealed class CheckInAppointmentCommandHandler
    : IRequestHandler<CheckInAppointmentCommand, AppointmentDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly TimeProvider _timeProvider;

    public CheckInAppointmentCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        TimeProvider timeProvider)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _timeProvider = timeProvider;
    }

    public async Task<AppointmentDto> Handle(
        CheckInAppointmentCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsInRole(Roles.Receptionist))
        {
            throw new ForbiddenException(
                "Only Receptionists can check in Appointments.");
        }

        return await _unitOfWork.ExecuteSerializableTransactionAsync(
            async token =>
            {
                var appointment = await _unitOfWork.Appointments.GetByIdAsync(
                    request.AppointmentId,
                    token);
                if (appointment is null)
                {
                    throw new NotFoundException("Appointment was not found.");
                }

                var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
                if (appointment.IsNoShowDue(utcNow))
                {
                    var nextAppointments = await _unitOfWork.Appointments.FindAsync(
                        candidate =>
                            candidate.DoctorId == appointment.DoctorId &&
                            candidate.AppointmentDate == appointment.AppointmentDate &&
                            candidate.Status == AppointmentStatus.Queued &&
                            candidate.QueuePosition > appointment.QueuePosition,
                        withTracking: false,
                        token);
                    if (appointment.SkipCount > 0 || nextAppointments.Any())
                    {
                        throw new BusinessRuleException(
                            "The call window has expired and the no-show must be processed first.");
                    }
                }

                try
                {
                    appointment.CheckIn(utcNow);
                }
                catch (InvalidOperationException exception)
                {
                    throw new BusinessRuleException(exception.Message);
                }

                await _unitOfWork.SaveChangesAsync(token);
                return AppointmentDto.FromEntity(appointment);
            },
            cancellationToken);
    }
}
