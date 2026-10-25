using HospitalCare.Application.Features.Appointments.DTOs;
using MediatR;

namespace HospitalCare.Application.Features.DoctorQueues.Commands;

public sealed record CallNextAppointmentCommand(
    int? DoctorId,
    DateOnly AppointmentDate) : IRequest<AppointmentDto>;
