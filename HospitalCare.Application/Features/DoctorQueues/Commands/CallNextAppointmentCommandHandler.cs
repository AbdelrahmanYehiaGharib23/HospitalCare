using HospitalCare.Application.Common.Constants;
using HospitalCare.Application.Common.Exceptions;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Features.Appointments.DTOs;
using HospitalCare.Domain.Enums;
using MediatR;

namespace HospitalCare.Application.Features.DoctorQueues.Commands;

public sealed class CallNextAppointmentCommandHandler
    : IRequestHandler<CallNextAppointmentCommand, AppointmentDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly TimeProvider _timeProvider;
    private readonly TimeZoneInfo _hospitalTimeZone;

    public CallNextAppointmentCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        TimeProvider timeProvider,
        TimeZoneInfo hospitalTimeZone)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _timeProvider = timeProvider;
        _hospitalTimeZone = hospitalTimeZone;
    }

    public async Task<AppointmentDto> Handle(
        CallNextAppointmentCommand request,
        CancellationToken cancellationToken)
    {
        var doctorId = GetDoctorId(request.DoctorId);
        EnsureToday(request.AppointmentDate);

        return await _unitOfWork.ExecuteSerializableTransactionAsync(
            token => CallNextAsync(doctorId, request.AppointmentDate, token),
            cancellationToken);
    }

    private int GetDoctorId(int? requestedDoctorId)
    {
        if (_currentUserService.IsInRole(Roles.Doctor))
        {
            if (!int.TryParse(_currentUserService.DoctorId, out var doctorId))
            {
                throw new ForbiddenException(
                    "The current Doctor account has no Doctor profile.");
            }

            return doctorId;
        }

        if (_currentUserService.IsInRole(Roles.Receptionist) &&
            requestedDoctorId.HasValue)
        {
            return requestedDoctorId.Value;
        }

        throw new ForbiddenException(
            "The current user cannot call a Doctor queue.");
    }

    private void EnsureToday(DateOnly appointmentDate)
    {
        var hospitalLocalNow = TimeZoneInfo.ConvertTime(
            _timeProvider.GetUtcNow(),
            _hospitalTimeZone);
        var today = DateOnly.FromDateTime(hospitalLocalNow.DateTime);
        if (appointmentDate != today)
        {
            throw new BusinessRuleException(
                "Only today's queue can be called.");
        }
    }

    private async Task<AppointmentDto> CallNextAsync(
        int doctorId,
        DateOnly appointmentDate,
        CancellationToken cancellationToken)
    {
        var doctors = await _unitOfWork.Doctors.FindAsync(
            doctor => doctor.Id == doctorId,
            withTracking: false,
            cancellationToken);
        if (!doctors.Any())
        {
            throw new NotFoundException("Doctor was not found.");
        }

        var appointments = (await _unitOfWork.Appointments.FindAsync(
            appointment =>
                appointment.DoctorId == doctorId &&
                appointment.AppointmentDate == appointmentDate &&
                (appointment.Status == AppointmentStatus.Queued ||
                    appointment.Status == AppointmentStatus.InProgress),
            withTracking: true,
            cancellationToken)).ToArray();
        if (appointments.Any(appointment =>
                appointment.Status == AppointmentStatus.InProgress ||
                appointment.CalledAt.HasValue))
        {
            throw new ConflictException(
                "The current queue turn must be started or processed first.");
        }

        var appointmentToCall = appointments
            .Where(appointment =>
                appointment.Status == AppointmentStatus.Queued &&
                appointment.CheckedInAt.HasValue)
            .MinBy(appointment => appointment.QueuePosition);
        if (appointmentToCall is null)
        {
            throw new BusinessRuleException(
                "There is no checked-in Appointment to call.");
        }

        try
        {
            appointmentToCall.Call(_timeProvider.GetUtcNow().UtcDateTime);
        }
        catch (InvalidOperationException exception)
        {
            throw new BusinessRuleException(exception.Message);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return AppointmentDto.FromEntity(appointmentToCall);
    }
}
