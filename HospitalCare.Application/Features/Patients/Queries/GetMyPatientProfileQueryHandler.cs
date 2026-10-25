using HospitalCare.Application.Common.Exceptions;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Features.Patients.DTOs;
using MediatR;

namespace HospitalCare.Application.Features.Patients.Queries.GetMyPatientProfile;

public sealed class GetMyPatientProfileQueryHandler
    : IRequestHandler<GetMyPatientProfileQuery, PatientDto>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public GetMyPatientProfileQueryHandler(
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task<PatientDto> Handle(
        GetMyPatientProfileQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated ||string.IsNullOrWhiteSpace(_currentUserService.PatientId))
        {
            throw new UnauthorizedAccessException(
                "The current patient could not be identified.");
        }

        if (!int.TryParse(
                _currentUserService.PatientId,
                out var patientId))
        {
            throw new UnauthorizedAccessException(
                "The patient identifier is invalid.");
        }

        var patient = await _unitOfWork.Patients.GetByIdAsync(
            patientId,
            cancellationToken);

        if (patient is null)
        {
            throw new NotFoundException("Patient profile not found.");
        }

        return PatientDto.FromEntity(patient);
    }
}
