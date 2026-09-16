using ATMTicketing.Application.DTOs;
using ATMTicketing.Application.Interfaces.Repositories;
using ATMTicketing.Application.Interfaces.Services;
using ATMTicketing.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ATMTicketing.Web.Controllers;

[Authorize(Roles = Roles.Administrator + "," + Roles.CallCenterAgent + "," + Roles.TeamLead + "," + Roles.OperationsManager)]
public class AtmController : Controller
{
    private readonly IAtmService _atmService;
    private readonly IUnitOfWork _uow;

    public AtmController(IAtmService atmService, IUnitOfWork uow)
    {
        _atmService = atmService;
        _uow = uow;
    }

    public IActionResult Index() => View();

    [HttpPost]
    public async Task<IActionResult> GetData([FromForm] DataTableRequest request, CancellationToken ct)
    {
        var result = await _atmService.GetPagedAsync(request, ct);
        return Ok(result);
    }

    [Authorize(Roles = Roles.Administrator + "," + Roles.TeamLead)]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateDropdownsAsync();
        return PartialView("_AtmForm", new AtmEditDto());
    }

    [Authorize(Roles = Roles.Administrator + "," + Roles.TeamLead)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AtmEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync();
            return PartialView("_AtmForm", dto);
        }

        var result = await _atmService.CreateAsync(dto, ct);
        return Json(result);
    }

    [Authorize(Roles = Roles.Administrator + "," + Roles.TeamLead)]
    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var dto = await _atmService.GetForEditAsync(id, ct);
        if (dto is null)
        {
            return NotFound();
        }
        await PopulateDropdownsAsync();
        return PartialView("_AtmForm", dto);
    }

    [Authorize(Roles = Roles.Administrator + "," + Roles.TeamLead)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(AtmEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync();
            return PartialView("_AtmForm", dto);
        }

        var result = await _atmService.UpdateAsync(dto, ct);
        return Json(result);
    }

    [Authorize(Roles = Roles.Administrator)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await _atmService.DeleteAsync(id, ct);
        return Json(result);
    }

    [Authorize(Roles = Roles.Administrator + "," + Roles.TeamLead)]
    [HttpGet]
    public IActionResult Import() => View();

    [Authorize(Roles = Roles.Administrator + "," + Roles.TeamLead)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            ModelState.AddModelError(string.Empty, "Please select an Excel (.xlsx) file to import.");
            return View();
        }

        await using var stream = file.OpenReadStream();
        var results = await _atmService.ImportFromExcelAsync(stream, ct);
        return View("ImportResult", results);
    }

    [HttpGet]
    public async Task<IActionResult> Search(string term, CancellationToken ct)
    {
        var results = await _atmService.SearchAsync(term ?? string.Empty, ct);
        return Ok(results);
    }

    private async Task PopulateDropdownsAsync()
    {
        var regions = await _uow.Regions.GetAllAsync();
        var vendors = await _uow.Vendors.GetAllAsync();
        ViewBag.Regions = new SelectList(regions.OrderBy(r => r.RegionName), "Id", "RegionName");
        ViewBag.Vendors = new SelectList(vendors.OrderBy(v => v.VendorName), "Id", "VendorName");
    }
}
