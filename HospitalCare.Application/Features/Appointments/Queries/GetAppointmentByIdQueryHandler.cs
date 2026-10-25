using HospitalCare.Application.Common.Exceptions;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Features.Appointments.DTOs;
using HospitalCare.Domain.Enums;
using MediatR;

namespace HospitalCare.Application.Features.Appointments.Queries;

public sealed class GetAppointmentByIdQueryHandler
    : IRequestHandler<GetAppointmentByIdQuery, PatientAppointmentDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public GetAppointmentByIdQueryHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<PatientAppointmentDto> Handle(
        GetAppointmentByIdQuery request,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(_currentUserService.PatientId, out var patientId))
        {
            throw new ForbiddenException(
                "The authenticated Patient profile was not found.");
        }

        var appointments = await _unitOfWork.Appointments.FindAsync(
            appointment => appointment.Id == request.AppointmentId,
            withTracking: false,
            cancellationToken);
        var appointment = appointments.SingleOrDefault();
        if (appointment is null)
        {
            throw new NotFoundException("Appointment was not found.");
        }

        if (appointment.PatientId != patientId)
        {
            throw new ForbiddenException(
                "Patients can only view their own Appointments.");
        }

        int? queuePosition = null;
        if (appointment.Status is AppointmentStatus.Queued or
            AppointmentStatus.InProgress)
        {
            var queue = await _unitOfWork.Appointments.FindAsync(
                candidate =>
                    candidate.DoctorId == appointment.DoctorId &&
                    candidate.AppointmentDate == appointment.AppointmentDate &&
                    (candidate.Status == AppointmentStatus.Queued ||
                        candidate.Status == AppointmentStatus.InProgress),
                withTracking: false,
                cancellationToken);
            queuePosition = queue.Count(candidate =>
                candidate.QueuePosition <= appointment.QueuePosition);
        }

        return PatientAppointmentDto.FromEntity(appointment, queuePosition);
    }
}
