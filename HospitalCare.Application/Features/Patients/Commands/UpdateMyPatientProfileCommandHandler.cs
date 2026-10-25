using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Features.Patients.DTOs;
using MediatR;

namespace HospitalCare.Application.Features.Patients.Commands;

public sealed class UpdateMyPatientProfileCommandHandler : IRequestHandler<UpdateMyPatientProfileCommand, PatientDto>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateMyPatientProfileCommandHandler(
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task<PatientDto> Handle(
        UpdateMyPatientProfileCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated ||
            !int.TryParse(_currentUserService.PatientId, out var patientId))
        {
            throw new UnauthorizedAccessException(
                "The current patient could not be identified.");
        }

        var patient = await _unitOfWork.Patients
            .GetByIdAsync(patientId, cancellationToken);

        if (patient is null)
        {
            throw new KeyNotFoundException("Patient profile not found.");
        }

        patient.Name = request.Name;
        patient.Phone = request.Phone;
        patient.DateOfBirth = request.DateOfBirth;
        patient.Allergies = request.Allergies;
        patient.ChronicConditions = request.ChronicConditions;

        _unitOfWork.Patients.Update(patient);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return PatientDto.FromEntity(patient);
    }
}
