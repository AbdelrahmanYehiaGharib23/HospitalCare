using HospitalCare.Application.Common.Exceptions;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Common.Models;
using HospitalCare.Application.Features.Appointments.DTOs;
using HospitalCare.Domain.Entities;
using HospitalCare.Domain.Enums;
using MediatR;

namespace HospitalCare.Application.Features.Appointments.Queries;

public sealed class GetPatientAppointmentsQueryHandler
    : IRequestHandler<
        GetPatientAppointmentsQuery,
        PagedResult<PatientAppointmentDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public GetPatientAppointmentsQueryHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<PagedResult<PatientAppointmentDto>> Handle(
        GetPatientAppointmentsQuery request,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(_currentUserService.PatientId, out var patientId))
        {
            throw new ForbiddenException(
                "The authenticated Patient profile was not found.");
        }

        var page = await _unitOfWork.Appointments.GetPageAsync(
            appointment => appointment.PatientId == patientId,
            request.PageNumber,
            request.PageSize,
            cancellationToken);
        var queueRanks = await GetQueueRanksAsync(
            page.Items,
            cancellationToken);
        var items = page.Items
            .Select(appointment =>
            {
                int? queuePosition = queueRanks.TryGetValue(
                    appointment.Id,
                    out var rank)
                    ? rank
                    : null;

                return PatientAppointmentDto.FromEntity(
                    appointment,
                    queuePosition);
            })
            .ToArray();

        return new PagedResult<PatientAppointmentDto>(
            items,
            page.PageNumber,
            page.PageSize,
            page.TotalCount);
    }

    private async Task<IReadOnlyDictionary<int, int>> GetQueueRanksAsync(
        IReadOnlyCollection<Appointment> appointments,
        CancellationToken cancellationToken)
    {
        var activeAppointments = appointments
            .Where(appointment => IsInQueue(appointment.Status))
            .ToArray();
        var ranks = new Dictionary<int, int>();

        foreach (var group in activeAppointments.GroupBy(
                     appointment =>
                         (appointment.DoctorId, appointment.AppointmentDate)))
        {
            var queue = await _unitOfWork.Appointments.FindAsync(
                appointment =>
                    appointment.DoctorId == group.Key.DoctorId &&
                    appointment.AppointmentDate == group.Key.AppointmentDate &&
                    (appointment.Status == AppointmentStatus.Queued ||
                        appointment.Status == AppointmentStatus.InProgress),
                withTracking: false,
                cancellationToken);
            var orderedQueue = queue
                .OrderBy(appointment => appointment.QueuePosition)
                .ToArray();

            foreach (var appointment in group)
            {
                ranks[appointment.Id] = orderedQueue
                    .TakeWhile(candidate =>
                        candidate.QueuePosition <= appointment.QueuePosition)
                    .Count();
            }
        }

        return ranks;
    }

    private static bool IsInQueue(AppointmentStatus status)
    {
        return status is AppointmentStatus.Queued or AppointmentStatus.InProgress;
    }
}
