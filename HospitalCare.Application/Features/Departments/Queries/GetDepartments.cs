using HospitalCare.Application.Common.Models;
using HospitalCare.Application.Features.Departments.DTOs;
using MediatR;

namespace HospitalCare.Application.Features.Departments.Queries
{
    public sealed record GetDepartmentsQuery(
        int PageNumber = 1,
        int PageSize = 20) : IRequest<PagedResult<DepartmentDto>>;
}
