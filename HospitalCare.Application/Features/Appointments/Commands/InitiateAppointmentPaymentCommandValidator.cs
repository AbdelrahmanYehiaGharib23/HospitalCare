using FluentValidation;

namespace HospitalCare.Application.Features.Appointments.Commands;

public sealed class InitiateAppointmentPaymentCommandValidator
    : AbstractValidator<InitiateAppointmentPaymentCommand>
{
    public InitiateAppointmentPaymentCommandValidator()
    {
        RuleFor(command => command.DoctorId)
            .GreaterThan(0);

        RuleFor(command => command.AppointmentTypeId)
            .GreaterThan(0);

        RuleFor(command => command.RequestedDate)
            .NotEqual(default(DateOnly));
    }
}
