using HospitalCare.Application.Common.Constants;
using HospitalCare.Application.Common.Models;
using HospitalCare.Application.Features.Doctors.Commands;
using HospitalCare.Application.Features.Doctors.DTOs;
using HospitalCare.Application.Features.Doctors.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
}
