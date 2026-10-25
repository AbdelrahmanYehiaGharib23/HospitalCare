using HospitalCare.Application.Common.Exceptions;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Features.DoctorSchedules.DTOs;
using HospitalCare.Domain.Entities;
using MediatR;

namespace HospitalCare.Application.Features.DoctorSchedules.Commands;

public sealed class UpdateDoctorScheduleCommandHandler
    : IRequestHandler<UpdateDoctorScheduleCommand, IReadOnlyList<DoctorScheduleDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public UpdateDoctorScheduleCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<IReadOnlyList<DoctorScheduleDto>> Handle(
        UpdateDoctorScheduleCommand request,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(_currentUserService.DoctorId, out var doctorId))
        {
            throw new ForbiddenException(
                "The authenticated Doctor profile was not found.");
        }

        var doctor = await _unitOfWork.Doctors.GetByIdAsync(
            doctorId,
            cancellationToken);

        if (doctor is null)
        {
            throw new NotFoundException("Doctor was not found.");
        }

        var existingSchedules = await _unitOfWork.DoctorSchedules.FindAsync(
            schedule => schedule.DoctorId == doctorId,
            withTracking: true,
            cancellationToken);

        foreach (var schedule in existingSchedules)
        {
            _unitOfWork.DoctorSchedules.Remove(schedule);
        }

        var replacements = request.Ranges
            .Select(range => new DoctorSchedule
            {
                DoctorId = doctorId,
                DayOfWeek = range.DayOfWeek,
                StartTime = range.StartTime,
                EndTime = range.EndTime
            })
            .ToArray();

        foreach (var schedule in replacements)
        {
            _unitOfWork.DoctorSchedules.Add(schedule);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return replacements
            .OrderBy(schedule => schedule.DayOfWeek)
            .ThenBy(schedule => schedule.StartTime)
            .Select(DoctorScheduleDto.FromEntity)
            .ToArray();
    }
}
