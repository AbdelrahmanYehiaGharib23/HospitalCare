using HospitalCare.Application.Common.Exceptions;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Features.Departments.DTOs;
using HospitalCare.Domain.Entities;
using MediatR;

namespace HospitalCare.Application.Features.Departments.Commands;

public sealed class CreateDepartmentCommandHandler
    : IRequestHandler<CreateDepartmentCommand, DepartmentDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreateDepartmentCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<DepartmentDto> Handle(
        CreateDepartmentCommand request,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        var existing = await _unitOfWork.Departments.FindAsync(
            department => department.Name.ToUpper() == name.ToUpper(),
            cancellationToken: cancellationToken);

        if (existing.Any())
        {
            throw new ConflictException(
                "A department with this name already exists.");
        }

        var department = new Department { Name = name };
        _unitOfWork.Departments.Add(department);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return DepartmentDto.FromEntity(department);
    }
}
