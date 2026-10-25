using System.Text;
using HospitalCare.Application.Features.Appointments.Commands;
using HospitalCare.Application.Features.Payments.DTOs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalCare.API.Controllers;

[ApiController]
[Route("api/payments")]
public sealed class PaymentsController : ControllerBase
{
    private readonly ISender _sender;

    public PaymentsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    [Consumes("application/json")]
    public async Task<ActionResult<PaymentWebhookResultDto>> HandleWebhook(
        [FromHeader(Name = "X-Payment-Signature")] string signature,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(
            Request.Body,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: true);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        var result = await _sender.Send(
            new ConfirmAppointmentPaymentCommand(payload, signature),
            cancellationToken);

        return Ok(result);
    }
}
