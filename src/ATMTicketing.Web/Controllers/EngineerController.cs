using ATMTicketing.Application.Interfaces.Services;
using ATMTicketing.Web.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ATMTicketing.Web.Controllers;

[Authorize(Roles = Domain.Entities.Roles.FieldEngineer + "," + Domain.Entities.Roles.Administrator)]
public class EngineerController : Controller
{
    private readonly ITicketService _ticketService;

    public EngineerController(ITicketService ticketService)
    {
        _ticketService = ticketService;
    }

    public async Task<IActionResult> Workbench(CancellationToken ct)
    {
        var engineerId = User.GetUserId();
        if (engineerId is null)
        {
            return Forbid();
        }

        var tickets = await _ticketService.GetMyTicketsAsync(engineerId, ct);
        return View(tickets);
    }
}
