using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Features.Departments.DTOs;
using MediatR;

namespace HospitalCare.Application.Features.Departments.Queries
{
    public sealed class GetDepartmentsQueryHandler : IRequestHandler<GetDepartmentsQuery, IEnumerable<DepartmentDto>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetDepartmentsQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<IEnumerable<DepartmentDto>> Handle(GetDepartmentsQuery request, CancellationToken cancellationToken)
        {
            var departments =
                await _unitOfWork.Departments.GetAllAsync(withTracking: false, cancellationToken);

            return departments.Select(DepartmentDto.FromEntity);
        }
    }
}