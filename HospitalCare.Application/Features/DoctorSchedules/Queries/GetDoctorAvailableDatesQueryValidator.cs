using FluentValidation;

namespace HospitalCare.Application.Features.DoctorSchedules.Queries;

public sealed class GetDoctorAvailableDatesQueryValidator
    : AbstractValidator<GetDoctorAvailableDatesQuery>
{
    private const int MaximumRangeDays = 90;

    public GetDoctorAvailableDatesQueryValidator()
    {
        RuleFor(query => query.DoctorId)
            .GreaterThan(0);

        RuleFor(query => query.AppointmentTypeId)
            .GreaterThan(0);

        RuleFor(query => query.To)
            .GreaterThanOrEqualTo(query => query.From);

        RuleFor(query => query)
            .Must(query =>
                query.To.DayNumber - query.From.DayNumber + 1 <=
                MaximumRangeDays)
            .WithMessage(
                $"The requested date range cannot exceed {MaximumRangeDays} days.");
    }
}
