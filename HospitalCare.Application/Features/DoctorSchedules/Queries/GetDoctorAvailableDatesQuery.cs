using HospitalCare.Application.Features.DoctorSchedules.DTOs;
using MediatR;

namespace HospitalCare.Application.Features.DoctorSchedules.Queries;

public sealed record GetDoctorAvailableDatesQuery(
    int DoctorId,
    int AppointmentTypeId,
    DateOnly From,
    DateOnly To) : IRequest<IReadOnlyList<DoctorAvailableDateDto>>;
