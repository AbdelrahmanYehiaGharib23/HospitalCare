using HospitalCare.Application.Common.Constants;
using HospitalCare.Application.Features.Appointments.Commands;
using HospitalCare.Application.Features.Payments.DTOs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalCare.API.Controllers;

[ApiController]
[Route("api/appointments")]
public sealed class AppointmentsController : ControllerBase
{
    private readonly ISender _sender;

    public AppointmentsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost("payments")]
    [Authorize(Roles = Roles.Patient)]
    public async Task<ActionResult<InitiatedAppointmentPaymentDto>>
        InitiateAppointmentPayment(
            [FromBody] InitiateAppointmentPaymentCommand command,
            CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return Ok(result);
    }
}
