using FluentValidation;

namespace HospitalCare.Application.Features.Appointments.Commands;

public sealed class CompleteAppointmentCommandValidator
    : AbstractValidator<CompleteAppointmentCommand>
{
    public CompleteAppointmentCommandValidator()
    {
        RuleFor(command => command.AppointmentId)
            .GreaterThan(0);

        RuleFor(command => command.Notes)
            .MaximumLength(2000);
    }
}
