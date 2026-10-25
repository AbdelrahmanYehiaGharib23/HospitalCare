using FluentValidation;

namespace HospitalCare.Application.Features.Doctors.Commands;

public sealed class DeactivateDoctorCommandValidator
    : AbstractValidator<DeactivateDoctorCommand>
{
    public DeactivateDoctorCommandValidator()
    {
        RuleFor(command => command.DoctorId)
            .GreaterThan(0);
    }
}
