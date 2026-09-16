using ATMTicketing.Application.DTOs;
using ATMTicketing.Application.Interfaces.Repositories;
using ATMTicketing.Application.Interfaces.Services;
using ATMTicketing.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ATMTicketing.Web.Controllers;

[Authorize(Roles = Roles.Administrator + "," + Roles.TeamLead + "," + Roles.OperationsManager)]
public class VendorController : Controller
{
    private readonly IVendorService _vendorService;
    private readonly IUnitOfWork _uow;

    public VendorController(IVendorService vendorService, IUnitOfWork uow)
    {
        _vendorService = vendorService;
        _uow = uow;
    }

    public IActionResult Index() => View();

    [HttpPost]
    public async Task<IActionResult> GetData([FromForm] DataTableRequest request, CancellationToken ct)
    {
        var result = await _vendorService.GetPagedAsync(request, ct);
        return Ok(result);
    }

    [Authorize(Roles = Roles.Administrator)]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateRegionsAsync();
        return PartialView("_VendorForm", new VendorEditDto());
    }

    [Authorize(Roles = Roles.Administrator)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(VendorEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await PopulateRegionsAsync();
            return PartialView("_VendorForm", dto);
        }

        var result = await _vendorService.CreateAsync(dto, ct);
        return Json(result);
    }

    [Authorize(Roles = Roles.Administrator)]
    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var dto = await _vendorService.GetForEditAsync(id, ct);
        if (dto is null)
        {
            return NotFound();
        }
        await PopulateRegionsAsync();
        return PartialView("_VendorForm", dto);
    }

    [Authorize(Roles = Roles.Administrator)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(VendorEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await PopulateRegionsAsync();
            return PartialView("_VendorForm", dto);
        }

        var result = await _vendorService.UpdateAsync(dto, ct);
        return Json(result);
    }

    [Authorize(Roles = Roles.Administrator)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await _vendorService.DeleteAsync(id, ct);
        return Json(result);
    }

    private async Task PopulateRegionsAsync()
    {
        var regions = await _uow.Regions.GetAllAsync();
        ViewBag.Regions = new SelectList(regions.OrderBy(r => r.RegionName), "Id", "RegionName");
    }
}
