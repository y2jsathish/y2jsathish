using ATMTicketing.Application.DTOs;
using ATMTicketing.Application.Interfaces.Repositories;
using ATMTicketing.Application.Interfaces.Services;
using ATMTicketing.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ATMTicketing.Web.Controllers;

/// <summary>Lets Administrators and Team Leads plan which Field Engineer covers which
/// region and shift, so the auto-assignment engine can follow that roster (and both roles
/// can still fall back to a manual Assign/Reassign on any ticket regardless of the roster).</summary>
[Authorize(Roles = Roles.Administrator + "," + Roles.TeamLead)]
public class DutyRosterController : Controller
{
    private readonly IDutyRosterService _dutyRosterService;
    private readonly IUnitOfWork _uow;
    private readonly UserManager<ApplicationUser> _userManager;

    public DutyRosterController(IDutyRosterService dutyRosterService, IUnitOfWork uow, UserManager<ApplicationUser> userManager)
    {
        _dutyRosterService = dutyRosterService;
        _uow = uow;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(DateOnly? from, DateOnly? to, int? regionId, CancellationToken ct)
    {
        var fromDate = from ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var toDate = to ?? fromDate.AddDays(13);
        var rows = await _dutyRosterService.GetAsync(fromDate, toDate, regionId, ct);

        ViewBag.From = fromDate;
        ViewBag.To = toDate;
        ViewBag.RegionId = regionId;
        ViewBag.Regions = new SelectList(await _uow.Regions.GetAllAsync(ct), "Id", "RegionName", regionId);
        return View(rows);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        await PopulateDropdownsAsync(ct);
        return PartialView("_DutyRosterForm", new DutyRosterEditDto());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DutyRosterEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync(ct);
            return PartialView("_DutyRosterForm", dto);
        }

        var result = await _dutyRosterService.CreateAsync(dto, ct);
        return Json(result);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var dto = await _dutyRosterService.GetForEditAsync(id, ct);
        if (dto is null)
        {
            return NotFound();
        }

        await PopulateDropdownsAsync(ct);
        return PartialView("_DutyRosterForm", dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(DutyRosterEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync(ct);
            return PartialView("_DutyRosterForm", dto);
        }

        var result = await _dutyRosterService.UpdateAsync(dto, ct);
        return Json(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await _dutyRosterService.DeleteAsync(id, ct);
        return Json(result);
    }

    private async Task PopulateDropdownsAsync(CancellationToken ct)
    {
        ViewBag.Regions = new SelectList(await _uow.Regions.GetAllAsync(ct), "Id", "RegionName");

        var engineers = (await _userManager.GetUsersInRoleAsync(Roles.FieldEngineer))
            .Where(u => u.IsActive)
            .OrderBy(u => u.FullName)
            .ToList();
        ViewBag.Engineers = new SelectList(engineers, "Id", "FullName");
    }
}
