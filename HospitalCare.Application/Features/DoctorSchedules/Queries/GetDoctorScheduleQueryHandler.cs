using HospitalCare.Application.Common.Constants;
using HospitalCare.Application.Common.Exceptions;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Features.DoctorSchedules.DTOs;
using MediatR;

namespace HospitalCare.Application.Features.DoctorSchedules.Queries;

public sealed class GetDoctorScheduleQueryHandler
    : IRequestHandler<GetDoctorScheduleQuery, IReadOnlyList<DoctorScheduleDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public GetDoctorScheduleQueryHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<IReadOnlyList<DoctorScheduleDto>> Handle(
        GetDoctorScheduleQuery request,
        CancellationToken cancellationToken)
    {
        var isAdmin = _currentUserService.IsInRole(Roles.Admin);
        var isDoctor = _currentUserService.IsInRole(Roles.Doctor);
        var isPatient = _currentUserService.IsInRole(Roles.Patient);
        var doctorId = GetDoctorId(
            request.DoctorId,
            isAdmin,
            isDoctor,
            isPatient);

        var doctors = await _unitOfWork.Doctors.FindAsync(
            doctor => doctor.Id == doctorId,
            withTracking: false,
            cancellationToken);
        var doctor = doctors.SingleOrDefault();

        if (doctor is null ||
            (!isAdmin && !isDoctor && !doctor.IsActive))
        {
            throw new NotFoundException("Doctor was not found.");
        }

        var schedules = await _unitOfWork.DoctorSchedules.FindAsync(
            schedule => schedule.DoctorId == doctorId,
            withTracking: false,
            cancellationToken);

        return schedules
            .OrderBy(schedule => schedule.DayOfWeek)
            .ThenBy(schedule => schedule.StartTime)
            .Select(DoctorScheduleDto.FromEntity)
            .ToArray();
    }

    private int GetDoctorId(
        int? requestedDoctorId,
        bool isAdmin,
        bool isDoctor,
        bool isPatient)
    {
        if (requestedDoctorId.HasValue)
        {
            if (isAdmin || isPatient)
            {
                return requestedDoctorId.Value;
            }

            throw new ForbiddenException(
                "Only Admins and Patients can request a Doctor ID.");
        }

        if (!isDoctor ||
            !int.TryParse(_currentUserService.DoctorId, out var doctorId))
        {
            throw new ForbiddenException(
                "The current Doctor profile was not found.");
        }

        return doctorId;
    }
}
