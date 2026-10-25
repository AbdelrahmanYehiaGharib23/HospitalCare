using HospitalCare.Application.Common.Models;
using HospitalCare.Application.Features.AppointmentTypes.DTOs;
using MediatR;

namespace HospitalCare.Application.Features.AppointmentTypes.Queries;

public sealed record GetAppointmentTypesQuery(
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResult<AppointmentTypeDto>>;
