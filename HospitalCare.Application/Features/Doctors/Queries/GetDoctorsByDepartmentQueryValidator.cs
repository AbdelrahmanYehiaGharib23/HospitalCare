using FluentValidation;

namespace HospitalCare.Application.Features.Doctors.Queries;

public sealed class GetDoctorsByDepartmentQueryValidator
    : AbstractValidator<GetDoctorsByDepartmentQuery>
{
    public GetDoctorsByDepartmentQueryValidator()
    {
        RuleFor(query => query.DepartmentId)
            .Must(departmentId =>
                !departmentId.HasValue || departmentId.Value > 0);

        RuleFor(query => query.PageNumber)
            .GreaterThan(0);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100);
    }
}
