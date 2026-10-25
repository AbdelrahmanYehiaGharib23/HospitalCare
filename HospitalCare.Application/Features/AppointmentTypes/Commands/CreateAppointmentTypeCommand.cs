using HospitalCare.Application.Features.AppointmentTypes.DTOs;
using MediatR;

namespace HospitalCare.Application.Features.AppointmentTypes.Commands;

public sealed record CreateAppointmentTypeCommand(
    string Name,
    int DurationMinutes,
    decimal BookingFee) : IRequest<AppointmentTypeDto>;
