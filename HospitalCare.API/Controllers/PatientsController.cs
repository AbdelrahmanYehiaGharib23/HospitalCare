using HospitalCare.Application.Features.Patients.Commands;
using HospitalCare.Application.Features.Patients.DTOs;
using HospitalCare.Application.Features.Patients.Queries.GetMyPatientProfile;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalCare.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Patient")]
public sealed class PatientsController : ControllerBase
{
    private readonly ISender _sender;

    public PatientsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetMyPatientProfileQuery(),
            cancellationToken);

        return Ok(result);
    }


    [HttpPut("me")]
    public async Task<ActionResult<PatientDto>> UpdateMyProfile(
        UpdateMyPatientProfileCommand command,
        CancellationToken cancellationToken)
    {
        var patientDto = await _sender.Send(command, cancellationToken);

        return Ok(patientDto);
    }


}
