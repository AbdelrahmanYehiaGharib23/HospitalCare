using FluentValidation;

namespace HospitalCare.Application.Features.Appointments.Commands;

public sealed class CancelAppointmentCommandValidator
    : AbstractValidator<CancelAppointmentCommand>
{
    public CancelAppointmentCommandValidator()
    {
        RuleFor(command => command.AppointmentId)
            .GreaterThan(0);
    }
}
