using HospitalCare.Application.Common.Exceptions;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Features.Doctors.DTOs;
using MediatR;

namespace HospitalCare.Application.Features.Doctors.Commands;

public sealed class CreateDoctorCommandHandler
    : IRequestHandler<CreateDoctorCommand, DoctorDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIdentityService _identityService;

    public CreateDoctorCommandHandler(
        IUnitOfWork unitOfWork,
        IIdentityService identityService)
    {
        _unitOfWork = unitOfWork;
        _identityService = identityService;
    }

    public async Task<DoctorDto> Handle(
        CreateDoctorCommand request,
        CancellationToken cancellationToken)
    {
        var department = await _unitOfWork.Departments.GetByIdAsync(
            request.DepartmentId,
            cancellationToken);

        if (department is null)
        {
            throw new NotFoundException("Department was not found.");
        }

        if (!department.IsActive || department.PendingDeactivation)
        {
            throw new ConflictException(
                "The Department is not accepting new Doctors.");
        }

        var doctor = await _identityService.CreateDoctorAsync(
            request.Name.Trim(),
            request.Email.Trim(),
            request.Password,
            request.DepartmentId,
            cancellationToken);

        return DoctorDto.FromEntity(doctor);
    }
}
