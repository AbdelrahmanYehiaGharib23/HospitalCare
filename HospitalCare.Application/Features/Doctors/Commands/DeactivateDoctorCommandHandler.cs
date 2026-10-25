using HospitalCare.Application.Common.Constants;
using HospitalCare.Application.Common.Exceptions;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Features.Doctors.DTOs;
using HospitalCare.Domain.Enums;
using MediatR;

namespace HospitalCare.Application.Features.Doctors.Commands;

public sealed class DeactivateDoctorCommandHandler
    : IRequestHandler<DeactivateDoctorCommand, DoctorDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public DeactivateDoctorCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<DoctorDto> Handle(
        DeactivateDoctorCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsInRole(Roles.Admin))
        {
            throw new ForbiddenException(
                "Only Admins can deactivate Doctors.");
        }

        return await _unitOfWork.ExecuteSerializableTransactionAsync(
            async token =>
            {
                var doctor = await _unitOfWork.Doctors.GetByIdAsync(
                    request.DoctorId,
                    token);
                if (doctor is null)
                {
                    throw new NotFoundException("Doctor was not found.");
                }

                var appointments = await _unitOfWork.Appointments.FindAsync(
                    appointment =>
                        appointment.DoctorId == doctor.Id &&
                        (appointment.Status == AppointmentStatus.Queued ||
                            appointment.Status == AppointmentStatus.InProgress),
                    withTracking: false,
                    token);
                doctor.RequestDeactivation(appointments.Any());
                await _unitOfWork.SaveChangesAsync(token);
                return DoctorDto.FromEntity(doctor);
            },
            cancellationToken);
    }
}
