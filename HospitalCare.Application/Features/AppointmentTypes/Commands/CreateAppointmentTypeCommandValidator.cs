using FluentValidation;

namespace HospitalCare.Application.Features.AppointmentTypes.Commands;

public sealed class CreateAppointmentTypeCommandValidator
    : AbstractValidator<CreateAppointmentTypeCommand>
{
    public CreateAppointmentTypeCommandValidator()
    {
        RuleFor(command => command.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Name is required.")
            .MaximumLength(150);

        RuleFor(command => command.DurationMinutes)
            .GreaterThan(0);

        RuleFor(command => command.BookingFee)
            .GreaterThan(0m)
            .LessThanOrEqualTo(9999999999999999.99m)
            .Must(bookingFee => decimal.Round(bookingFee, 2) == bookingFee)
            .WithMessage("Booking fee must have no more than two decimal places.");
    }
}
