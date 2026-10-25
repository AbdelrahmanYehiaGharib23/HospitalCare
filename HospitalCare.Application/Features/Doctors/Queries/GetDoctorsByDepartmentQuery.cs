using HospitalCare.Application.Common.Models;
using HospitalCare.Application.Features.Doctors.DTOs;
using MediatR;

namespace HospitalCare.Application.Features.Doctors.Queries;

public sealed record GetDoctorsByDepartmentQuery(
    int? DepartmentId = null,
    bool IncludeInactive = false,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResult<DoctorDto>>;
