using HospitalCare.API.Contracts.Departments;
using HospitalCare.Application.Common.Constants;
using HospitalCare.Application.Common.Models;
using HospitalCare.Application.Features.Departments.Commands;
using HospitalCare.Application.Features.Departments.DTOs;
using HospitalCare.Application.Features.Departments.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalCare.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class DepartmentsController : ControllerBase
{
    private readonly ISender _sender;

    public DepartmentsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    [Authorize(Roles = Roles.Patient)]
    public async Task<ActionResult<PagedResult<DepartmentDto>>> GetDepartments(
        [FromQuery] GetDepartmentsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<DepartmentDto>> CreateDepartment(
        [FromBody] CreateDepartmentCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<DepartmentDto>> UpdateDepartment(
        int id,
        [FromBody] UpdateDepartmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new UpdateDepartmentCommand(id, request.Name),
            cancellationToken);

        return Ok(result);
    }
}
