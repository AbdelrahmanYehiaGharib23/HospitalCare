using System;
using System.Collections.Generic;
using System.Text;
using HospitalCare.Application.Features.Departments.DTOs;
using MediatR;

namespace HospitalCare.Application.Features.Departments.Queries
{
    public sealed record GetDepartmentsQuery  : IRequest<IEnumerable<DepartmentDto>>;
}
