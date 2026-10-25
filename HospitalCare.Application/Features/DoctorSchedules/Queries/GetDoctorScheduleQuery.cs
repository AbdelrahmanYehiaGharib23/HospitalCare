using HospitalCare.Application.Features.DoctorSchedules.DTOs;
using MediatR;

namespace HospitalCare.Application.Features.DoctorSchedules.Queries;

public sealed record GetDoctorScheduleQuery(int? DoctorId)
    : IRequest<IReadOnlyList<DoctorScheduleDto>>;
