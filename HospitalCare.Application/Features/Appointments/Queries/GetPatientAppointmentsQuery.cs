using HospitalCare.Application.Common.Models;
using HospitalCare.Application.Features.Appointments.DTOs;
using MediatR;

namespace HospitalCare.Application.Features.Appointments.Queries;

public sealed record GetPatientAppointmentsQuery(
    int PageNumber = 1,
    int PageSize = 20)
    : IRequest<PagedResult<PatientAppointmentDto>>;
