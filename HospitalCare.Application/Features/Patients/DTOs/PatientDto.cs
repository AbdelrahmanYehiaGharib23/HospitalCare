using HospitalCare.Domain.Entities;

namespace HospitalCare.Application.Features.Patients.DTOs
{
    public sealed record PatientDto(int Id,string Name,string Email, string Phone, DateOnly DateOfBirth, string? Allergies,string? ChronicConditions)
    {
        public static PatientDto FromEntity(Patient patient)
        {
            return new PatientDto(
                patient.Id,
                patient.Name,
                patient.Email,
                patient.Phone,
                patient.DateOfBirth,
                patient.Allergies,
                patient.ChronicConditions);
        }
    }
}