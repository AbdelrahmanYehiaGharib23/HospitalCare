using HospitalCare.Application.Features.Departments.DTOs;
using MediatR;

namespace HospitalCare.Application.Features.Departments.Commands;

public sealed record UpdateDepartmentCommand(int Id, string Name)
    : IRequest<DepartmentDto>;
