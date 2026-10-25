using FluentValidation;

namespace HospitalCare.Application.Features.DoctorSchedules.Queries;

public sealed class GetDoctorAvailableDatesQueryValidator
    : AbstractValidator<GetDoctorAvailableDatesQuery>
{
    public GetDoctorAvailableDatesQueryValidator()
    {
        RuleFor(query => query.DoctorId)
            .GreaterThan(0);

        RuleFor(query => query.AppointmentTypeId)
            .GreaterThan(0);

        RuleFor(query => query.To)
            .GreaterThanOrEqualTo(query => query.From);
    }
}
