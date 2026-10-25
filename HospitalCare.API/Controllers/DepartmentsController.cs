using HospitalCare.Application.Common.Constants;
using HospitalCare.Application.Common.Models;
using HospitalCare.Application.Features.Departments.DTOs;
using HospitalCare.Application.Features.Departments.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalCare.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Patient)]
public sealed class DepartmentsController : ControllerBase
{
    private readonly ISender _sender;

    public DepartmentsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<DepartmentDto>>> GetDepartments(
        [FromQuery] GetDepartmentsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }
}
