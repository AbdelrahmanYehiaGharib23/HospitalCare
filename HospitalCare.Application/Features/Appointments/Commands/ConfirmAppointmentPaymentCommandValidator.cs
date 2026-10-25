using FluentValidation;

namespace HospitalCare.Application.Features.Appointments.Commands;

public sealed class ConfirmAppointmentPaymentCommandValidator
    : AbstractValidator<ConfirmAppointmentPaymentCommand>
{
    public ConfirmAppointmentPaymentCommandValidator()
    {
        RuleFor(command => command.Payload)
            .NotEmpty();

        RuleFor(command => command.Signature)
            .NotEmpty();
    }
}
