using MediatR;

namespace HospitalCare.Application.Features.Appointments.Commands;

public sealed record ProcessNoShowSwapCommand(int AppointmentId)
    : IRequest<bool>;
