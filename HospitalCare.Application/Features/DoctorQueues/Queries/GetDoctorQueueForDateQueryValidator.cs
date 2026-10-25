using FluentValidation;

namespace HospitalCare.Application.Features.DoctorQueues.Queries;

public sealed class GetDoctorQueueForDateQueryValidator
    : AbstractValidator<GetDoctorQueueForDateQuery>
{
    private const int MaximumPageSize = 100;

    public GetDoctorQueueForDateQueryValidator()
    {
        RuleFor(query => query.DoctorId)
            .GreaterThan(0);

        RuleFor(query => query.Date)
            .NotEqual(default(DateOnly));

        RuleFor(query => query.PageNumber)
            .GreaterThan(0);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, MaximumPageSize);
    }
}
