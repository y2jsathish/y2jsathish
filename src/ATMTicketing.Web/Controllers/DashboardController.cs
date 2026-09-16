using ATMTicketing.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ATMTicketing.Web.Controllers;

[Authorize]
public class DashboardController : Controller
{
    public IActionResult Index() => View();
}

[Authorize]
[Route("api/dashboard")]
[ApiController]
public class DashboardApiController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardApiController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken ct) => Ok(await _dashboardService.GetSummaryAsync(ct));

    [HttpGet("engineer-workload")]
    public async Task<IActionResult> EngineerWorkload(CancellationToken ct) => Ok(await _dashboardService.GetEngineerWorkloadAsync(ct));

    [HttpGet("region-wise")]
    public async Task<IActionResult> RegionWise(CancellationToken ct) => Ok(await _dashboardService.GetRegionWiseTicketsAsync(ct));

    [HttpGet("priority-wise")]
    public async Task<IActionResult> PriorityWise(CancellationToken ct) => Ok(await _dashboardService.GetPriorityWiseTicketsAsync(ct));

    [HttpGet("vendor-wise")]
    public async Task<IActionResult> VendorWise(CancellationToken ct) => Ok(await _dashboardService.GetVendorWiseTicketsAsync(ct));

    [HttpGet("daily-trend")]
    public async Task<IActionResult> DailyTrend([FromQuery] int days = 14, CancellationToken ct = default) =>
        Ok(await _dashboardService.GetDailyTrendAsync(days, ct));

    [HttpGet("monthly-trend")]
    public async Task<IActionResult> MonthlyTrend([FromQuery] int months = 6, CancellationToken ct = default) =>
        Ok(await _dashboardService.GetMonthlyTrendAsync(months, ct));
}
