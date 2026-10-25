using FluentValidation;

namespace HospitalCare.Application.Features.Appointments.Commands;

public sealed class StartAppointmentCommandValidator
    : AbstractValidator<StartAppointmentCommand>
{
    public StartAppointmentCommandValidator()
    {
        RuleFor(command => command.AppointmentId)
            .GreaterThan(0);
    }
}
