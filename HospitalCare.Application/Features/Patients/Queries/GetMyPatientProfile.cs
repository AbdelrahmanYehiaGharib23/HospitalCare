using HospitalCare.Application.Features.Patients.DTOs;
using MediatR;

namespace HospitalCare.Application.Features.Patients.Queries.GetMyPatientProfile;

public sealed record GetMyPatientProfileQuery: IRequest<PatientDto>;