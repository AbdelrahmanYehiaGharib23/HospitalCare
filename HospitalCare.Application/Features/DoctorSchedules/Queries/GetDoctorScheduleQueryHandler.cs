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

        if (!isAdmin && !isDoctor && !isPatient)
        {
            throw new ForbiddenException(
                "The current user cannot view Doctor schedules.");
        }

        if (isDoctor && !isAdmin)
        {
            if (!int.TryParse(
                    _currentUserService.DoctorId,
                    out var currentDoctorId))
            {
                throw new ForbiddenException(
                    "The authenticated Doctor profile was not found.");
            }

            if (currentDoctorId != request.DoctorId)
            {
                throw new ForbiddenException(
                    "Doctors can only view their own schedule.");
            }
        }

        var doctors = await _unitOfWork.Doctors.FindAsync(
            doctor => doctor.Id == request.DoctorId,
            withTracking: false,
            cancellationToken);
        var doctor = doctors.SingleOrDefault();

        if (doctor is null ||
            (!isAdmin && !isDoctor && !doctor.IsActive))
        {
            throw new NotFoundException("Doctor was not found.");
        }

        var schedules = await _unitOfWork.DoctorSchedules.FindAsync(
            schedule => schedule.DoctorId == request.DoctorId,
            withTracking: false,
            cancellationToken);

        return schedules
            .OrderBy(schedule => schedule.DayOfWeek)
            .ThenBy(schedule => schedule.StartTime)
            .Select(DoctorScheduleDto.FromEntity)
            .ToArray();
    }
}
