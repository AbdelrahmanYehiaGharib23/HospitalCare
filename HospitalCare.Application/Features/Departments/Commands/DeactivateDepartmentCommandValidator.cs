using FluentValidation;

namespace HospitalCare.Application.Features.Departments.Commands;

public sealed class DeactivateDepartmentCommandValidator
    : AbstractValidator<DeactivateDepartmentCommand>
{
    public DeactivateDepartmentCommandValidator()
    {
        RuleFor(command => command.DepartmentId)
            .GreaterThan(0);
    }
}
