using HospitalCare.Application.Common.Constants;
using HospitalCare.Application.Features.Patients.Commands;
using HospitalCare.Application.Features.Patients.DTOs;
using HospitalCare.Application.Features.Patients.Queries;
using HospitalCare.Application.Features.Patients.Queries.GetMyPatientProfile;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalCare.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class PatientsController : ControllerBase
{
    private readonly ISender _sender;

    public PatientsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("me")]
    [Authorize(Roles = Roles.Patient)]
    public async Task<IActionResult> GetMyProfile(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetMyPatientProfileQuery(),
            cancellationToken);

        return Ok(result);
    }


    [HttpPut("me")]
    [Authorize(Roles = Roles.Patient)]
    public async Task<ActionResult<PatientDto>> UpdateMyProfile(
        UpdateMyPatientProfileCommand command,
        CancellationToken cancellationToken)
    {
        var patientDto = await _sender.Send(command, cancellationToken);

        return Ok(patientDto);
    }

    [HttpGet("me/medical-history")]
    [Authorize(Roles = Roles.Patient)]
    public async Task<ActionResult<PatientMedicalHistoryDto>>
        GetMyMedicalHistory(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetPatientMedicalHistoryQuery(null),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:int}/medical-history")]
    [Authorize(Roles = Roles.Doctor + "," + Roles.Receptionist)]
    public async Task<ActionResult<PatientMedicalHistoryDto>>
        GetPatientMedicalHistory(
            int id,
            CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetPatientMedicalHistoryQuery(id),
            cancellationToken);

        return Ok(result);
    }
}
