using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Common.Models;
using HospitalCare.Application.Features.AppointmentTypes.DTOs;
using MediatR;

namespace HospitalCare.Application.Features.AppointmentTypes.Queries;

public sealed class GetAppointmentTypesQueryHandler
    : IRequestHandler<GetAppointmentTypesQuery, PagedResult<AppointmentTypeDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetAppointmentTypesQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<AppointmentTypeDto>> Handle(
        GetAppointmentTypesQuery request,
        CancellationToken cancellationToken)
    {
        var appointmentTypes = await _unitOfWork.AppointmentTypes.GetPageAsync(
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        return new PagedResult<AppointmentTypeDto>(
            appointmentTypes.Items
                .Select(AppointmentTypeDto.FromEntity)
                .ToArray(),
            appointmentTypes.PageNumber,
            appointmentTypes.PageSize,
            appointmentTypes.TotalCount);
    }
}
