using FluentValidation;

namespace HospitalCare.Application.Features.Departments.Queries;

public sealed class GetDepartmentsQueryValidator
    : AbstractValidator<GetDepartmentsQuery>
{
    public GetDepartmentsQueryValidator()
    {
        RuleFor(query => query.PageNumber)
            .GreaterThan(0);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100);
    }
}
