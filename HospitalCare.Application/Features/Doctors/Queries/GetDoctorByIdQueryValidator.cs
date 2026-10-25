using FluentValidation;

namespace HospitalCare.Application.Features.Doctors.Queries;

public sealed class GetDoctorByIdQueryValidator
    : AbstractValidator<GetDoctorByIdQuery>
{
    public GetDoctorByIdQueryValidator()
    {
        RuleFor(query => query.Id)
            .GreaterThan(0);
    }
}
