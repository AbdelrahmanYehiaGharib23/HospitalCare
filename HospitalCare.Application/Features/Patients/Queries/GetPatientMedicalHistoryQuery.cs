using HospitalCare.Application.Features.Patients.DTOs;
using MediatR;

namespace HospitalCare.Application.Features.Patients.Queries;

public sealed record GetPatientMedicalHistoryQuery(int? PatientId)
    : IRequest<PatientMedicalHistoryDto>;
