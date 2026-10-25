using HospitalCare.Application.Common.Constants;
using HospitalCare.Application.Common.Models;
using HospitalCare.Application.Features.Doctors.Commands;
using HospitalCare.Application.Features.Doctors.DTOs;
using HospitalCare.Application.Features.Doctors.Queries;
using HospitalCare.Application.Features.DoctorSchedules.Commands;
using HospitalCare.Application.Features.DoctorSchedules.DTOs;
using HospitalCare.Application.Features.DoctorSchedules.Queries;
using HospitalCare.Application.Features.DoctorQueues.DTOs;
using HospitalCare.Application.Features.DoctorQueues.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace HospitalCare.API.Controllers;

[ApiController]
[Route("api/doctors")]
public sealed class DoctorsController : ControllerBase
{
    private readonly ISender _sender;

    public DoctorsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    [Authorize(Roles = Roles.Admin + "," + Roles.Patient)]
    public async Task<ActionResult<PagedResult<DoctorDto>>> GetDoctors(
        [FromQuery] GetDoctorsByDepartmentQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Patient)]
    public async Task<ActionResult<DoctorDto>> GetDoctorById(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetDoctorByIdQuery(id),
            cancellationToken);

        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<DoctorDto>> CreateDoctor(
        [FromBody] CreateDoctorCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}/schedule")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Doctor + "," + Roles.Patient)]
    public async Task<ActionResult<IReadOnlyList<DoctorScheduleDto>>> GetDoctorSchedule(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetDoctorScheduleQuery(id),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:int}/available-dates")]
    [Authorize(Roles = Roles.Patient)]
    public async Task<ActionResult<IReadOnlyList<DoctorAvailableDateDto>>> GetAvailableDates(
        int id,
        [FromQuery] int appointmentTypeId,
        [FromQuery, BindRequired] DateOnly from,
        [FromQuery, BindRequired] DateOnly to,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetDoctorAvailableDatesQuery(
                id,
                appointmentTypeId,
                from,
                to),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:int}/queue")]
    [Authorize(Roles = Roles.Doctor + "," + Roles.Receptionist)]
    public async Task<ActionResult<PagedResult<DoctorQueueItemDto>>> GetQueueForDate(
        int id,
        [FromQuery, BindRequired] DateOnly date,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new GetDoctorQueueForDateQuery(
                id,
                date,
                pageNumber,
                pageSize),
            cancellationToken);

        return Ok(result);
    }

    [HttpPut("me/schedule")]
    [Authorize(Roles = Roles.Doctor)]
    public async Task<ActionResult<IReadOnlyList<DoctorScheduleDto>>> UpdateMySchedule(
        [FromBody] UpdateDoctorScheduleCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return Ok(result);
    }
}
