using System.Linq.Expressions;
using HospitalCare.Application.Common.Constants;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Common.Models;
using HospitalCare.Application.Features.Doctors.DTOs;
using HospitalCare.Domain.Entities;
using MediatR;

namespace HospitalCare.Application.Features.Doctors.Queries;

public sealed class GetDoctorsByDepartmentQueryHandler
    : IRequestHandler<GetDoctorsByDepartmentQuery, PagedResult<DoctorDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public GetDoctorsByDepartmentQueryHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<PagedResult<DoctorDto>> Handle(
        GetDoctorsByDepartmentQuery request,
        CancellationToken cancellationToken)
    {
        var includeInactive =
            request.IncludeInactive &&
            _currentUserService.IsInRole(Roles.Admin);

        Expression<Func<Doctor, bool>> predicate = doctor =>
            (!request.DepartmentId.HasValue ||
                doctor.DepartmentId == request.DepartmentId.Value) &&
            (includeInactive || doctor.IsActive);

        var doctors = await _unitOfWork.Doctors.GetPageAsync(
            predicate,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        return new PagedResult<DoctorDto>(
            doctors.Items.Select(DoctorDto.FromEntity).ToArray(),
            doctors.PageNumber,
            doctors.PageSize,
            doctors.TotalCount);
    }
}
