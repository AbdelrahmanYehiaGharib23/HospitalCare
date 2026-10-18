using HospitalCare.Application.Common.Models;
using MediatR;

namespace HospitalCare.Application.Authentication;

public sealed record RegisterPatientCommand(
    string Name,
    string Email,
    string Password,
    string Phone,
    DateOnly DateOfBirth,
    string? Allergies = null,
    string? ChronicConditions = null
) : IRequest<JwtTokenResult>;
