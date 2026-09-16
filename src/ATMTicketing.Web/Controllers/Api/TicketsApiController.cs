using ATMTicketing.Application.DTOs;
using ATMTicketing.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ATMTicketing.Web.Controllers.Api;

/// <summary>JWT-secured REST surface for tickets, for external/mobile integrations.</summary>
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[ApiController]
[Route("api/tickets")]
public class TicketsApiController : ControllerBase
{
    private readonly ITicketService _ticketService;

    public TicketsApiController(ITicketService ticketService)
    {
        _ticketService = ticketService;
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var ticket = await _ticketService.GetDetailsAsync(id, ct);
        return ticket is null ? NotFound() : Ok(ticket);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TicketCreateDto dto, CancellationToken ct)
    {
        var result = await _ticketService.CreateAsync(dto, ct);
        return result.Succeeded ? CreatedAtAction(nameof(Get), new { id = result.Data!.Id }, result.Data) : BadRequest(result);
    }

    [HttpPost("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] TicketStatusUpdateDto dto, CancellationToken ct)
    {
        if (id != dto.TicketId)
        {
            return BadRequest("Route id and body TicketId must match.");
        }
        var result = await _ticketService.UpdateStatusAsync(dto, ct);
        return result.Succeeded ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id:int}/assign")]
    public async Task<IActionResult> Assign(int id, [FromBody] TicketAssignDto dto, CancellationToken ct)
    {
        if (id != dto.TicketId)
        {
            return BadRequest("Route id and body TicketId must match.");
        }
        var result = await _ticketService.AssignAsync(dto, ct);
        return result.Succeeded ? Ok(result) : BadRequest(result);
    }

    [HttpGet("my")]
    public async Task<IActionResult> MyTickets(CancellationToken ct)
    {
        var engineerId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (engineerId is null)
        {
            return Unauthorized();
        }
        return Ok(await _ticketService.GetMyTicketsAsync(engineerId, ct));
    }
}
