using HospitalCare.Application.Features.DoctorSchedules.DTOs;
using MediatR;

namespace HospitalCare.Application.Features.DoctorSchedules.Commands;

public sealed record UpdateDoctorScheduleCommand(
    IReadOnlyList<DoctorScheduleRange> Ranges)
    : IRequest<IReadOnlyList<DoctorScheduleDto>>;
