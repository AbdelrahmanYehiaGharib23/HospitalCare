using HospitalCare.Application.Features.Payments.DTOs;
using MediatR;

namespace HospitalCare.Application.Features.Appointments.Commands;

public sealed record InitiateAppointmentPaymentCommand(
    int DoctorId,
    int AppointmentTypeId,
    DateOnly RequestedDate)
    : IRequest<InitiatedAppointmentPaymentDto>;
