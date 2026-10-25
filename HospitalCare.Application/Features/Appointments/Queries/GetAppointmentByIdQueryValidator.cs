using FluentValidation;

namespace HospitalCare.Application.Features.Appointments.Queries;

public sealed class GetAppointmentByIdQueryValidator
    : AbstractValidator<GetAppointmentByIdQuery>
{
    public GetAppointmentByIdQueryValidator()
    {
        RuleFor(query => query.AppointmentId)
            .GreaterThan(0);
    }
}
