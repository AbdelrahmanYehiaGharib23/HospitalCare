using FluentValidation;

namespace HospitalCare.Application.Features.DoctorQueues.Commands;

public sealed class CallNextAppointmentCommandValidator
    : AbstractValidator<CallNextAppointmentCommand>
{
    public CallNextAppointmentCommandValidator()
    {
        RuleFor(command => command.DoctorId)
            .GreaterThan(0)
            .When(command => command.DoctorId.HasValue);

        RuleFor(command => command.AppointmentDate)
            .NotEqual(default(DateOnly));
    }
}
