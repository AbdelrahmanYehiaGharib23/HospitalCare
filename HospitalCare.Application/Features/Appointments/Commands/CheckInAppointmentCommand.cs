using HospitalCare.Application.Features.Appointments.DTOs;
using MediatR;

namespace HospitalCare.Application.Features.Appointments.Commands;

public sealed record CheckInAppointmentCommand(int AppointmentId)
    : IRequest<AppointmentDto>;
