using ATMTicketing.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ATMTicketing.Web.Controllers;

[Authorize(Policy = "CanAssignTickets")]
public class TicketAssignmentController : Controller
{
    private readonly IDashboardService _dashboardService;

    public TicketAssignmentController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    public async Task<IActionResult> Index()
    {
        var workload = await _dashboardService.GetEngineerWorkloadAsync();
        return View(workload);
    }
}
