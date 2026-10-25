using HospitalCare.Application.Common.Exceptions;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Features.Departments.DTOs;
using MediatR;

namespace HospitalCare.Application.Features.Departments.Commands;

public sealed class UpdateDepartmentCommandHandler
    : IRequestHandler<UpdateDepartmentCommand, DepartmentDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public UpdateDepartmentCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<DepartmentDto> Handle(
        UpdateDepartmentCommand request,
        CancellationToken cancellationToken)
    {
        var department = await _unitOfWork.Departments.GetByIdAsync(
            request.Id,
            cancellationToken);

        if (department is null)
        {
            throw new NotFoundException("Department was not found.");
        }

        var name = request.Name.Trim();
        var existing = await _unitOfWork.Departments.FindAsync(
            candidate =>
                candidate.Id != request.Id &&
                candidate.Name.ToUpper() == name.ToUpper(),
            cancellationToken: cancellationToken);

        if (existing.Any())
        {
            throw new ConflictException(
                "A department with this name already exists.");
        }

        department.Name = name;
        _unitOfWork.Departments.Update(department);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return DepartmentDto.FromEntity(department);
    }
}
