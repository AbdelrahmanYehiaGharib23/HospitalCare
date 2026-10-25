using FluentValidation;

namespace HospitalCare.Application.Features.Receptionists.Commands;

public sealed class CreateReceptionistAccountCommandValidator
    : AbstractValidator<CreateReceptionistAccountCommand>
{
    public CreateReceptionistAccountCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        RuleFor(command => command.Password)
            .NotEmpty();
    }
}
