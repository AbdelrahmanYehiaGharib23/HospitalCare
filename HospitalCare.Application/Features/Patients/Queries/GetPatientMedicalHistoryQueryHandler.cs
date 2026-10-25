using HospitalCare.Application.Common.Constants;
using HospitalCare.Application.Common.Exceptions;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Features.Patients.DTOs;
using MediatR;

namespace HospitalCare.Application.Features.Patients.Queries;

public sealed class GetPatientMedicalHistoryQueryHandler
    : IRequestHandler<
        GetPatientMedicalHistoryQuery,
        PatientMedicalHistoryDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public GetPatientMedicalHistoryQueryHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<PatientMedicalHistoryDto> Handle(
        GetPatientMedicalHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var patientId = await GetPatientIdAsync(request, cancellationToken);
        var patients = await _unitOfWork.Patients.FindAsync(
            patient => patient.Id == patientId,
            withTracking: false,
            cancellationToken);
        var patient = patients.SingleOrDefault();
        if (patient is null)
        {
            throw new NotFoundException("Patient was not found.");
        }

        return PatientMedicalHistoryDto.FromEntity(patient);
    }

    private async Task<int> GetPatientIdAsync(
        GetPatientMedicalHistoryQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.IsInRole(Roles.Patient))
        {
            if (request.PatientId.HasValue ||
                !int.TryParse(_currentUserService.PatientId, out var patientId))
            {
                throw new ForbiddenException(
                    "Patients can only view their own medical history.");
            }

            return patientId;
        }

        if (!request.PatientId.HasValue)
        {
            throw new ForbiddenException(
                "The current user cannot view this medical history.");
        }

        if (_currentUserService.IsInRole(Roles.Receptionist))
        {
            return request.PatientId.Value;
        }

        if (!_currentUserService.IsInRole(Roles.Doctor) ||
            !int.TryParse(_currentUserService.DoctorId, out var doctorId))
        {
            throw new ForbiddenException(
                "The current user cannot view this medical history.");
        }

        var appointments = await _unitOfWork.Appointments.FindAsync(
            appointment =>
                appointment.DoctorId == doctorId &&
                appointment.PatientId == request.PatientId.Value,
            withTracking: false,
            cancellationToken);
        if (!appointments.Any())
        {
            throw new ForbiddenException(
                "Doctors can only view the medical history of their own patients.");
        }

        return request.PatientId.Value;
    }
}
