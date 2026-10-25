using FluentValidation;

namespace HospitalCare.Application.Features.Patients.Queries;

public sealed class GetPatientMedicalHistoryQueryValidator
    : AbstractValidator<GetPatientMedicalHistoryQuery>
{
    public GetPatientMedicalHistoryQueryValidator()
    {
        RuleFor(query => query.PatientId)
            .GreaterThan(0)
            .When(query => query.PatientId.HasValue);
    }
}
