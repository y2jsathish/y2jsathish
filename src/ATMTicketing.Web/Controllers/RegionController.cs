using ATMTicketing.Application.DTOs;
using ATMTicketing.Application.Interfaces.Services;
using ATMTicketing.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ATMTicketing.Web.Controllers;

[Authorize(Roles = Roles.Administrator)]
public class RegionController : Controller
{
    private readonly IRegionService _regionService;

    public RegionController(IRegionService regionService)
    {
        _regionService = regionService;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var regions = await _regionService.GetAllAsync(ct);
        return View(regions);
    }

    [HttpGet]
    public IActionResult Create() => PartialView("_RegionForm", new RegionEditDto());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RegionEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return PartialView("_RegionForm", dto);
        }

        var result = await _regionService.CreateAsync(dto, ct);
        return Json(result);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var dto = await _regionService.GetForEditAsync(id, ct);
        return dto is null ? NotFound() : PartialView("_RegionForm", dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(RegionEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return PartialView("_RegionForm", dto);
        }

        var result = await _regionService.UpdateAsync(dto, ct);
        return Json(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await _regionService.DeleteAsync(id, ct);
        return Json(result);
    }
}
