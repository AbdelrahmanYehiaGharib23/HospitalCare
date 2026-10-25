using FluentValidation;

namespace HospitalCare.Application.Features.Appointments.Queries;

public sealed class GetPatientAppointmentsQueryValidator
    : AbstractValidator<GetPatientAppointmentsQuery>
{
    private const int MaximumPageSize = 100;

    public GetPatientAppointmentsQueryValidator()
    {
        RuleFor(query => query.PageNumber)
            .GreaterThan(0);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, MaximumPageSize);
    }
}
