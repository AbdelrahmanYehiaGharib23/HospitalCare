using HospitalCare.API.Contracts.Appointments;
using HospitalCare.Application.Common.Constants;
using HospitalCare.Application.Common.Models;
using HospitalCare.Application.Features.Appointments.Commands;
using HospitalCare.Application.Features.Appointments.DTOs;
using HospitalCare.Application.Features.Appointments.Queries;
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

    [HttpPost("{id:int}/cancel")]
    [Authorize(
        Roles = Roles.Patient + "," + Roles.Receptionist + "," + Roles.Admin)]
    public async Task<ActionResult<CancelledAppointmentDto>> CancelAppointment(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new CancelAppointmentCommand(id),
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("{id:int}/check-in")]
    [Authorize(Roles = Roles.Receptionist)]
    public async Task<ActionResult<AppointmentDto>> CheckInAppointment(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new CheckInAppointmentCommand(id),
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("{id:int}/start")]
    [Authorize(Roles = Roles.Doctor)]
    public async Task<ActionResult<AppointmentDto>> StartAppointment(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new StartAppointmentCommand(id),
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("{id:int}/complete")]
    [Authorize(Roles = Roles.Doctor)]
    public async Task<ActionResult<AppointmentDto>> CompleteAppointment(
        int id,
        [FromBody] CompleteAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new CompleteAppointmentCommand(id, request.Notes),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("me")]
    [Authorize(Roles = Roles.Patient)]
    public async Task<ActionResult<PagedResult<PatientAppointmentDto>>>
        GetMyAppointments(
            [FromQuery] GetPatientAppointmentsQuery query,
            CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = Roles.Patient)]
    public async Task<ActionResult<PatientAppointmentDto>> GetAppointmentById(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetAppointmentByIdQuery(id),
            cancellationToken);

        return Ok(result);
    }
}
