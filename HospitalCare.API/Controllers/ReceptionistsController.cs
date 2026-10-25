using HospitalCare.Application.Common.Constants;
using HospitalCare.Application.Features.Receptionists.Commands;
using HospitalCare.Application.Features.Receptionists.DTOs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalCare.API.Controllers;

[ApiController]
[Route("api/receptionists")]
[Authorize(Roles = Roles.Admin)]
public sealed class ReceptionistsController : ControllerBase
{
    private readonly ISender _sender;

    public ReceptionistsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    public async Task<ActionResult<ReceptionistAccountDto>> CreateReceptionist(
        [FromBody] CreateReceptionistAccountCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return Ok(result);
    }
}
