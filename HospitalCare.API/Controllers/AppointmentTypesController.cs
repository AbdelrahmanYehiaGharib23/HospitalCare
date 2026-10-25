using HospitalCare.API.Contracts.AppointmentTypes;
using HospitalCare.Application.Common.Constants;
using HospitalCare.Application.Common.Models;
using HospitalCare.Application.Features.AppointmentTypes.Commands;
using HospitalCare.Application.Features.AppointmentTypes.DTOs;
using HospitalCare.Application.Features.AppointmentTypes.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalCare.API.Controllers;

[ApiController]
[Route("api/appointment-types")]
public sealed class AppointmentTypesController : ControllerBase
{
    private readonly ISender _sender;

    public AppointmentTypesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    [Authorize(Roles = Roles.Admin + "," + Roles.Patient)]
    public async Task<ActionResult<PagedResult<AppointmentTypeDto>>> GetAppointmentTypes(
        [FromQuery] GetAppointmentTypesQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<AppointmentTypeDto>> CreateAppointmentType(
        [FromBody] CreateAppointmentTypeCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<AppointmentTypeDto>> UpdateAppointmentType(
        int id,
        [FromBody] UpdateAppointmentTypeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new UpdateAppointmentTypeCommand(
                id,
                request.Name,
                request.DurationMinutes,
                request.BookingFee),
            cancellationToken);

        return Ok(result);
    }
}
