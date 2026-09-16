using ATMTicketing.Application.DTOs;
using ATMTicketing.Application.Interfaces.Services;
using ATMTicketing.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ATMTicketing.Web.Controllers;

[Authorize(Policy = "CanViewReports")]
public class SlaController : Controller
{
    private readonly ISlaService _slaService;

    public SlaController(ISlaService slaService)
    {
        _slaService = slaService;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var dashboard = await _slaService.GetSlaDashboardAsync(ct);
        return View(dashboard);
    }

    [Authorize(Roles = Roles.Administrator)]
    [HttpGet]
    public async Task<IActionResult> Configuration(CancellationToken ct)
    {
        var configs = await _slaService.GetConfigurationsAsync(ct);
        return View(configs);
    }

    [Authorize(Roles = Roles.Administrator)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Configuration(SlaConfigurationDto dto, CancellationToken ct)
    {
        var result = await _slaService.UpdateConfigurationAsync(dto, ct);
        return Json(result);
    }

    [HttpGet]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        var dashboard = await _slaService.GetSlaDashboardAsync(ct);
        return Ok(dashboard);
    }
}
