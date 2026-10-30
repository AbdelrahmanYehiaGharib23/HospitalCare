using HospitalCare.Application.Common.Constants;
using HospitalCare.Application.Common.Exceptions;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Common.Models;
using HospitalCare.Application.Features.DoctorQueues.DTOs;
using HospitalCare.Domain.Entities;
using HospitalCare.Domain.Enums;
using HospitalCare.Domain.Services;
using MediatR;

namespace HospitalCare.Application.Features.DoctorQueues.Queries;

public sealed class GetDoctorQueueForDateQueryHandler
    : IRequestHandler<
        GetDoctorQueueForDateQuery,
        PagedResult<DoctorQueueItemDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly QueueEstimator _queueEstimator;
    private readonly TimeProvider _timeProvider;
    private readonly TimeZoneInfo _hospitalTimeZone;

    public GetDoctorQueueForDateQueryHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        QueueEstimator queueEstimator,
        TimeProvider timeProvider,
        TimeZoneInfo hospitalTimeZone)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _queueEstimator = queueEstimator;
        _timeProvider = timeProvider;
        _hospitalTimeZone = hospitalTimeZone;
    }

    public async Task<PagedResult<DoctorQueueItemDto>> Handle(
        GetDoctorQueueForDateQuery request,
        CancellationToken cancellationToken)
    {
        var doctorId = GetDoctorId(request.DoctorId);

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
                appointment.AppointmentDate == request.Date &&
                (appointment.Status == AppointmentStatus.Completed ||
                    appointment.Status == AppointmentStatus.Queued ||
                    appointment.Status == AppointmentStatus.InProgress),
            withTracking: false,
            cancellationToken))
            .OrderBy(appointment => appointment.QueuePosition)
            .ToArray();
        var appointmentTypes = await GetAppointmentTypesAsync(
            appointments,
            cancellationToken);
        var hospitalLocalNow = TimeZoneInfo.ConvertTime(
            _timeProvider.GetUtcNow(),
            _hospitalTimeZone);
        var schedules = (await _unitOfWork.DoctorSchedules.FindAsync(
            schedule =>
                schedule.DoctorId == doctorId &&
                schedule.DayOfWeek == request.Date.DayOfWeek,
            withTracking: false,
            cancellationToken))
            .OrderBy(schedule => schedule.StartTime)
            .ToArray();
        var estimates = schedules.Length == 0
            ? new Dictionary<int, QueueEstimate>()
            : EstimateQueue(
                    request.Date,
                    hospitalLocalNow,
                    schedules[0].StartTime,
                    appointments,
                    appointmentTypes)
                .ToDictionary(estimate => estimate.AppointmentId);
        var queue = appointments
            .Where(appointment => IsInQueue(appointment.Status))
            .ToArray();
        var patients = await GetPatientsAsync(queue, cancellationToken);
        var skip = (long)(request.PageNumber - 1) * request.PageSize;
        var page = skip >= queue.Length
            ? Array.Empty<Appointment>()
            : queue.Skip((int)skip).Take(request.PageSize).ToArray();
        var items = page
            .Select(appointment =>
            {
                estimates.TryGetValue(appointment.Id, out var estimate);
                var patient = patients[appointment.PatientId];

                return new DoctorQueueItemDto(
                    appointment.Id,
                    patient.Id,
                    patient.Name,
                    appointment.AppointmentTypeId,
                    appointment.QueuePosition,
                    appointment.Status,
                    estimate?.EstimatedStartTime,
                    estimate?.EstimatedDelayMinutes,
                    appointment.CalledAt,
                    appointment.CheckedInAt);
            })
            .ToArray();

        return new PagedResult<DoctorQueueItemDto>(
            items,
            request.PageNumber,
            request.PageSize,
            queue.Length);
    }

    private int GetDoctorId(int? requestedDoctorId)
    {
        if (requestedDoctorId.HasValue &&
            _currentUserService.IsInRole(Roles.Receptionist))
        {
            return requestedDoctorId.Value;
        }

        if (requestedDoctorId.HasValue ||
            !_currentUserService.IsInRole(Roles.Doctor) ||
            !int.TryParse(_currentUserService.DoctorId, out var currentDoctorId))
        {
            throw new ForbiddenException(
                "The current user cannot view this Doctor queue.");
        }

        return currentDoctorId;
    }

    private async Task<Dictionary<int, AppointmentType>> GetAppointmentTypesAsync(
        IReadOnlyCollection<Appointment> appointments,
        CancellationToken cancellationToken)
    {
        var appointmentTypeIds = appointments
            .Select(appointment => appointment.AppointmentTypeId)
            .Distinct()
            .ToArray();
        if (appointmentTypeIds.Length == 0)
        {
            return new Dictionary<int, AppointmentType>();
        }

        return (await _unitOfWork.AppointmentTypes.FindAsync(
                appointmentType => appointmentTypeIds.Contains(appointmentType.Id),
                withTracking: false,
                cancellationToken))
            .ToDictionary(appointmentType => appointmentType.Id);
    }

    private async Task<Dictionary<int, Patient>> GetPatientsAsync(
        IReadOnlyCollection<Appointment> appointments,
        CancellationToken cancellationToken)
    {
        var patientIds = appointments
            .Select(appointment => appointment.PatientId)
            .Distinct()
            .ToArray();
        if (patientIds.Length == 0)
        {
            return new Dictionary<int, Patient>();
        }

        return (await _unitOfWork.Patients.FindAsync(
                patient => patientIds.Contains(patient.Id),
                withTracking: false,
                cancellationToken))
            .ToDictionary(patient => patient.Id);
    }

    private IReadOnlyList<QueueEstimate> EstimateQueue(
        DateOnly date,
        DateTimeOffset hospitalLocalNow,
        TimeOnly scheduleStartTime,
        IReadOnlyCollection<Appointment> appointments,
        IReadOnlyDictionary<int, AppointmentType> appointmentTypes)
    {
        var localScheduleStart = date.ToDateTime(scheduleStartTime);
        var scheduleStartUtc = TimeZoneInfo.ConvertTimeToUtc(
            localScheduleStart,
            _hospitalTimeZone);
        var scheduleStart = TimeZoneInfo.ConvertTime(
            new DateTimeOffset(scheduleStartUtc),
            _hospitalTimeZone);
        var estimatorAppointments = appointments
            .Select(appointment => new QueueEstimatorAppointment(
                appointment.Id,
                appointment.QueuePosition,
                appointment.Status,
                appointmentTypes[appointment.AppointmentTypeId]
                    .DurationMinutes,
                ToHospitalLocalTime(appointment.ActualStartTime),
                ToHospitalLocalTime(appointment.ActualEndTime)))
            .ToArray();

        return _queueEstimator.Estimate(
            scheduleStart,
            hospitalLocalNow,
            estimatorAppointments);
    }

    private DateTimeOffset? ToHospitalLocalTime(DateTime? utcTime)
    {
        if (!utcTime.HasValue)
        {
            return null;
        }

        var utc = new DateTimeOffset(
            DateTime.SpecifyKind(utcTime.Value, DateTimeKind.Utc));
        return TimeZoneInfo.ConvertTime(utc, _hospitalTimeZone);
    }

    private static bool IsInQueue(AppointmentStatus status)
    {
        return status is AppointmentStatus.Queued or
            AppointmentStatus.InProgress;
    }
}
