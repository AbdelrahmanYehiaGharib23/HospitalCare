using HospitalCare.Application.Features.Appointments.DTOs;
using MediatR;

namespace HospitalCare.Application.Features.Appointments.Queries;

public sealed record GetAppointmentByIdQuery(int AppointmentId)
    : IRequest<PatientAppointmentDto>;
