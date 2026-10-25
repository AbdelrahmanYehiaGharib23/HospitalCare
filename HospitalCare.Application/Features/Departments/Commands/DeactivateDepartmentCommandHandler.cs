using HospitalCare.Application.Common.Constants;
using HospitalCare.Application.Common.Exceptions;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Features.Departments.DTOs;
using HospitalCare.Domain.Enums;
using MediatR;

namespace HospitalCare.Application.Features.Departments.Commands;

public sealed class DeactivateDepartmentCommandHandler
    : IRequestHandler<DeactivateDepartmentCommand, DepartmentDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public DeactivateDepartmentCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<DepartmentDto> Handle(
        DeactivateDepartmentCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsInRole(Roles.Admin))
        {
            throw new ForbiddenException(
                "Only Admins can deactivate Departments.");
        }

        return await _unitOfWork.ExecuteSerializableTransactionAsync(
            token => DeactivateWithinTransactionAsync(request, token),
            cancellationToken);
    }

    private async Task<DepartmentDto> DeactivateWithinTransactionAsync(
        DeactivateDepartmentCommand request,
        CancellationToken cancellationToken)
    {
        var department = await _unitOfWork.Departments.GetByIdAsync(
            request.DepartmentId,
            cancellationToken);
        if (department is null)
        {
            throw new NotFoundException("Department was not found.");
        }

        if (!department.IsActive)
        {
            return DepartmentDto.FromEntity(department);
        }

        var doctors = (await _unitOfWork.Doctors.FindAsync(
            doctor => doctor.DepartmentId == department.Id,
            withTracking: true,
            cancellationToken)).ToArray();
        var doctorIds = doctors.Select(doctor => doctor.Id).ToArray();
        var appointments = doctorIds.Length == 0
            ? []
            : (await _unitOfWork.Appointments.FindAsync(
                appointment =>
                    doctorIds.Contains(appointment.DoctorId) &&
                    (appointment.Status == AppointmentStatus.Queued ||
                        appointment.Status == AppointmentStatus.InProgress),
                withTracking: false,
                cancellationToken)).ToArray();
        var doctorsWithAppointments = appointments
            .Select(appointment => appointment.DoctorId)
            .ToHashSet();

        department.RequestDeactivation(appointments.Length > 0);
        foreach (var doctor in doctors)
        {
            doctor.RequestDeactivation(
                doctorsWithAppointments.Contains(doctor.Id));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return DepartmentDto.FromEntity(department);
    }
}
