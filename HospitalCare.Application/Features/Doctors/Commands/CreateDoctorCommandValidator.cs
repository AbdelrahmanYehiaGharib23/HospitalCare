using FluentValidation;

namespace HospitalCare.Application.Features.Doctors.Commands;

public sealed class CreateDoctorCommandValidator
    : AbstractValidator<CreateDoctorCommand>
{
    public CreateDoctorCommandValidator()
    {
        RuleFor(command => command.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Name is required.")
            .MaximumLength(200);

        RuleFor(command => command.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        RuleFor(command => command.Password)
            .NotEmpty();

        RuleFor(command => command.DepartmentId)
            .GreaterThan(0);
    }
}
