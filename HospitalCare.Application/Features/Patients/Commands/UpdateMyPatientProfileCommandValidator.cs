using FluentValidation;

namespace HospitalCare.Application.Features.Patients.Authentication;

public sealed class UpdateMyPatientProfileCommandValidator
    : AbstractValidator<UpdateMyPatientProfileCommand>
{
    public UpdateMyPatientProfileCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Phone)
            .NotEmpty()
            .MaximumLength(30);

        RuleFor(x => x.DateOfBirth)
            .LessThan(DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Date of birth must be in the past.");

        RuleFor(x => x.Allergies)
            .MaximumLength(2000);

        RuleFor(x => x.ChronicConditions)
            .MaximumLength(2000);
    }
}
