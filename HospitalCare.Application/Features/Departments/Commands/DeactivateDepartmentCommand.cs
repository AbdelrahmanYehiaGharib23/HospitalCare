using HospitalCare.Application.Features.Departments.DTOs;
using MediatR;

namespace HospitalCare.Application.Features.Departments.Commands;

public sealed record DeactivateDepartmentCommand(int DepartmentId)
    : IRequest<DepartmentDto>;
