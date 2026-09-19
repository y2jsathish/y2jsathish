using ATMTicketing.Application.DTOs;
using ATMTicketing.Application.Interfaces.Services;
using ATMTicketing.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ATMTicketing.Web.Controllers;

[Authorize(Roles = Roles.Administrator)]
public class CategoryController : Controller
{
    private readonly ICategoryService _categoryService;

    public CategoryController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var categories = await _categoryService.GetAllAsync(ct);
        return View(categories);
    }

    [HttpGet]
    public IActionResult Create() => PartialView("_CategoryForm", new CategoryEditDto());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CategoryEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return PartialView("_CategoryForm", dto);
        }

        var result = await _categoryService.CreateAsync(dto, ct);
        return Json(result);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var dto = await _categoryService.GetForEditAsync(id, ct);
        return dto is null ? NotFound() : PartialView("_CategoryForm", dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CategoryEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return PartialView("_CategoryForm", dto);
        }

        var result = await _categoryService.UpdateAsync(dto, ct);
        return Json(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await _categoryService.DeleteAsync(id, ct);
        return Json(result);
    }
}
