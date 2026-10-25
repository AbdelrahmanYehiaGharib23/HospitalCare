using HospitalCare.Application.Common.Constants;
using HospitalCare.Application.Features.Doctors.Commands;
using HospitalCare.Application.Features.Doctors.DTOs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalCare.API.Controllers;

[ApiController]
[Route("api/doctors")]
[Authorize(Roles = Roles.Admin)]
public sealed class DoctorsController : ControllerBase
{
    private readonly ISender _sender;

    public DoctorsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    public async Task<ActionResult<DoctorDto>> CreateDoctor(
        [FromBody] CreateDoctorCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return Ok(result);
    }
}
