using HospitalCare.Application.Common.Models;
using HospitalCare.Application.Features.DoctorQueues.DTOs;
using MediatR;

namespace HospitalCare.Application.Features.DoctorQueues.Queries;

public sealed record GetDoctorQueueForDateQuery(
    int DoctorId,
    DateOnly Date,
    int PageNumber = 1,
    int PageSize = 20)
    : IRequest<PagedResult<DoctorQueueItemDto>>;
