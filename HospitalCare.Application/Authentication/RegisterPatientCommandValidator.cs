
using FluentValidation;

namespace HospitalCare.Application.Authentication;

public sealed class RegisterPatientCommandValidator: AbstractValidator<RegisterPatientCommand>
{
    public RegisterPatientCommandValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty();

        RuleFor(x => x.Phone)
            .NotEmpty()
            .MaximumLength(30);

        RuleFor(x => x.DateOfBirth)
            .Must(dateOfBirth =>
                dateOfBirth < DateOnly.FromDateTime(
                    timeProvider.GetUtcNow().UtcDateTime))
            .WithMessage("Date of birth must be in the past.");

        RuleFor(x => x.Allergies)
            .MaximumLength(2000);

        RuleFor(x => x.ChronicConditions)
            .MaximumLength(2000);
    }
}
