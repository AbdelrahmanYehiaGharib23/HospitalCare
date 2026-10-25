using FluentValidation;

namespace HospitalCare.Application.Features.DoctorSchedules.Queries;

public sealed class GetDoctorScheduleQueryValidator
    : AbstractValidator<GetDoctorScheduleQuery>
{
    public GetDoctorScheduleQueryValidator()
    {
        RuleFor(query => query.DoctorId)
            .Must(doctorId => !doctorId.HasValue || doctorId.Value > 0);
    }
}
