using HospitalCare.Application.Features.Patients.DTOs;
using MediatR;

namespace HospitalCare.Application.Features.Patients.Commands;

public sealed record UpdateMyPatientProfileCommand(
    string Name,
    string Phone,
    DateOnly DateOfBirth,
    string? Allergies,
    string? ChronicConditions
) : IRequest<PatientDto>;
