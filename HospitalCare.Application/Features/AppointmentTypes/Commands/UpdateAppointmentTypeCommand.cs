using HospitalCare.Application.Features.AppointmentTypes.DTOs;
using MediatR;

namespace HospitalCare.Application.Features.AppointmentTypes.Commands;

public sealed record UpdateAppointmentTypeCommand(
    int Id,
    string Name,
    int DurationMinutes,
    decimal BookingFee) : IRequest<AppointmentTypeDto>;
