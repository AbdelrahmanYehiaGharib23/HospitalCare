using FluentValidation;

namespace HospitalCare.Application.Features.Appointments.Commands;

public sealed class ProcessNoShowSwapCommandValidator
    : AbstractValidator<ProcessNoShowSwapCommand>
{
    public ProcessNoShowSwapCommandValidator()
    {
        RuleFor(command => command.AppointmentId)
            .GreaterThan(0);
    }
}
