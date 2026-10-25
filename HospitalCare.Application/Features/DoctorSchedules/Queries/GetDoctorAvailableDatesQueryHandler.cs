using HospitalCare.Application.Common.Exceptions;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Features.DoctorSchedules.DTOs;
using HospitalCare.Domain.Enums;
using HospitalCare.Domain.Services;
using MediatR;

namespace HospitalCare.Application.Features.DoctorSchedules.Queries;

public sealed class GetDoctorAvailableDatesQueryHandler
    : IRequestHandler<
        GetDoctorAvailableDatesQuery,
        IReadOnlyList<DoctorAvailableDateDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly DoctorCapacityCalculator _capacityCalculator;
    private readonly TimeProvider _timeProvider;
    private readonly TimeZoneInfo _hospitalTimeZone;

    public GetDoctorAvailableDatesQueryHandler(
        IUnitOfWork unitOfWork,
        DoctorCapacityCalculator capacityCalculator,
        TimeProvider timeProvider,
        TimeZoneInfo hospitalTimeZone)
    {
        _unitOfWork = unitOfWork;
        _capacityCalculator = capacityCalculator;
        _timeProvider = timeProvider;
        _hospitalTimeZone = hospitalTimeZone;
    }

    public async Task<IReadOnlyList<DoctorAvailableDateDto>> Handle(
        GetDoctorAvailableDatesQuery request,
        CancellationToken cancellationToken)
    {
        var doctors = await _unitOfWork.Doctors.FindAsync(
            doctor => doctor.Id == request.DoctorId,
            withTracking: false,
            cancellationToken);
        var doctor = doctors.SingleOrDefault();

        if (doctor is null || !doctor.IsActive)
        {
            throw new NotFoundException("Doctor was not found.");
        }

        if (doctor.PendingDeactivation)
        {
            return [];
        }

        var departments = await _unitOfWork.Departments.FindAsync(
            department => department.Id == doctor.DepartmentId,
            withTracking: false,
            cancellationToken);
        var department = departments.SingleOrDefault();
        if (department is null || !department.IsActive)
        {
            throw new NotFoundException("Department was not found.");
        }

        if (department.PendingDeactivation)
        {
            return [];
        }

        var appointmentTypes = await _unitOfWork.AppointmentTypes.FindAsync(
            appointmentType =>
                appointmentType.Id == request.AppointmentTypeId,
            withTracking: false,
            cancellationToken);
        var appointmentType = appointmentTypes.SingleOrDefault();

        if (appointmentType is null)
        {
            throw new NotFoundException("Appointment type was not found.");
        }

        var schedules = (await _unitOfWork.DoctorSchedules.FindAsync(
            schedule => schedule.DoctorId == request.DoctorId,
            withTracking: false,
            cancellationToken)).ToArray();

        var appointments = (await _unitOfWork.Appointments.FindAsync(
            appointment =>
                appointment.DoctorId == request.DoctorId &&
                appointment.AppointmentDate >= request.From &&
                appointment.AppointmentDate <= request.To &&
                (appointment.Status == AppointmentStatus.Queued ||
                    appointment.Status == AppointmentStatus.InProgress),
            withTracking: false,
            cancellationToken)).ToArray();

        var appointmentTypeIds = appointments
            .Select(appointment => appointment.AppointmentTypeId)
            .Distinct()
            .ToArray();
        var appointmentTypeDurations = appointmentTypeIds.Length == 0
            ? new Dictionary<int, int>()
            : (await _unitOfWork.AppointmentTypes.FindAsync(
                    appointmentType =>
                        appointmentTypeIds.Contains(appointmentType.Id),
                    withTracking: false,
                    cancellationToken))
                .ToDictionary(
                    appointmentType => appointmentType.Id,
                    appointmentType => appointmentType.DurationMinutes);
        var capacityAppointments = appointments
            .Select(appointment => new DoctorCapacityAppointment(
                appointment.AppointmentDate,
                appointment.Status,
                appointmentTypeDurations[appointment.AppointmentTypeId]))
            .ToArray();
        var hospitalLocalNow = TimeZoneInfo.ConvertTime(
            _timeProvider.GetUtcNow(),
            _hospitalTimeZone);
        var availableDates = new List<DoctorAvailableDateDto>();

        for (var date = request.From; ; date = date.AddDays(1))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var capacity = _capacityCalculator.Calculate(
                date,
                schedules,
                capacityAppointments,
                appointmentType.DurationMinutes,
                hospitalLocalNow);

            if (capacity.CanFit)
            {
                availableDates.Add(new DoctorAvailableDateDto(date));
            }

            if (date == request.To)
            {
                break;
            }
        }

        return availableDates;
    }
}
