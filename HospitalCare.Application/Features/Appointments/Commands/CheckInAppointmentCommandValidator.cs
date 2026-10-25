using FluentValidation;

namespace HospitalCare.Application.Features.Appointments.Commands;

public sealed class CheckInAppointmentCommandValidator
    : AbstractValidator<CheckInAppointmentCommand>
{
    public CheckInAppointmentCommandValidator()
    {
        RuleFor(command => command.AppointmentId)
            .GreaterThan(0);
    }
}
