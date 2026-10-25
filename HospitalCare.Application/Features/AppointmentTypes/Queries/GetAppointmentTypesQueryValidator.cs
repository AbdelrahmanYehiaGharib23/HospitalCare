using FluentValidation;

namespace HospitalCare.Application.Features.AppointmentTypes.Queries;

public sealed class GetAppointmentTypesQueryValidator
    : AbstractValidator<GetAppointmentTypesQuery>
{
    public GetAppointmentTypesQueryValidator()
    {
        RuleFor(query => query.PageNumber)
            .GreaterThan(0);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100);
    }
}
