using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Common.Models;
using HospitalCare.Application.Features.Departments.DTOs;
using MediatR;

namespace HospitalCare.Application.Features.Departments.Queries
{
    public sealed class GetDepartmentsQueryHandler
        : IRequestHandler<GetDepartmentsQuery, PagedResult<DepartmentDto>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetDepartmentsQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<PagedResult<DepartmentDto>> Handle(
            GetDepartmentsQuery request,
            CancellationToken cancellationToken)
        {
            var departments = await _unitOfWork.Departments.GetPageAsync(
                request.PageNumber,
                request.PageSize,
                cancellationToken);

            return new PagedResult<DepartmentDto>(
                departments.Items.Select(DepartmentDto.FromEntity).ToArray(),
                departments.PageNumber,
                departments.PageSize,
                departments.TotalCount);
        }
    }
}
