using HospitalCare.Domain.Entities;

namespace HospitalCare.Application.Features.Patients.DTOs;

public sealed record PatientMedicalHistoryDto(
    int PatientId,
    string? Allergies,
    string? ChronicConditions)
{
    public static PatientMedicalHistoryDto FromEntity(Patient patient)
    {
        return new PatientMedicalHistoryDto(
            patient.Id,
            patient.Allergies,
            patient.ChronicConditions);
    }
}
