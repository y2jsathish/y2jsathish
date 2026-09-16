using ATMTicketing.Application.DTOs;
using ATMTicketing.Application.Interfaces.Repositories;
using ATMTicketing.Application.Interfaces.Services;
using ATMTicketing.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ATMTicketing.Web.Controllers;

[Authorize]
public class TicketController : Controller
{
    private static readonly string[] AllowedAttachmentExtensions = { ".jpg", ".jpeg", ".png", ".pdf", ".docx", ".xlsx" };
    private const long MaxAttachmentBytes = 10 * 1024 * 1024; // 10 MB

    private readonly ITicketService _ticketService;
    private readonly IUnitOfWork _uow;
    private readonly IWebHostEnvironment _environment;

    public TicketController(ITicketService ticketService, IUnitOfWork uow, IWebHostEnvironment environment)
    {
        _ticketService = ticketService;
        _uow = uow;
        _environment = environment;
    }

    public IActionResult Index() => View();

    [HttpPost]
    public async Task<IActionResult> GetData([FromForm] DataTableRequest request, [FromForm] TicketFilterDto filter, CancellationToken ct)
    {
        var result = await _ticketService.GetPagedAsync(request, filter, ct);
        return Ok(result);
    }

    [Authorize(Policy = "CanManageTickets")]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateDropdownsAsync();
        return View(new TicketCreateDto());
    }

    [Authorize(Policy = "CanManageTickets")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TicketCreateDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync();
            return View(dto);
        }

        var result = await _ticketService.CreateAsync(dto, ct);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, string.Join(" ", result.Errors));
            await PopulateDropdownsAsync();
            return View(dto);
        }

        TempData["Success"] = result.Message;
        return RedirectToAction(nameof(Details), new { id = result.Data!.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var details = await _ticketService.GetDetailsAsync(id, ct);
        if (details is null)
        {
            return NotFound();
        }

        ViewBag.Statuses = await _uow.Statuses.GetAllAsync(ct);
        return View(details);
    }

    [Authorize(Policy = "CanWorkTickets")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(TicketStatusUpdateDto dto, CancellationToken ct)
    {
        var result = await _ticketService.UpdateStatusAsync(dto, ct);
        return Json(result);
    }

    [Authorize(Policy = "CanAssignTickets")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(TicketAssignDto dto, CancellationToken ct)
    {
        var result = await _ticketService.AssignAsync(dto, ct);
        return Json(result);
    }

    [Authorize(Policy = "CanAssignTickets")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Escalate(int ticketId, string reason, CancellationToken ct)
    {
        var result = await _ticketService.EscalateAsync(ticketId, reason, ct);
        return Json(result);
    }

    [Authorize(Policy = "CanWorkTickets")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(TicketCloseDto dto, CancellationToken ct)
    {
        var result = await _ticketService.CloseAsync(dto, ct);
        return Json(result);
    }

    [Authorize(Policy = "CanWorkTickets")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxAttachmentBytes)]
    public async Task<IActionResult> UploadAttachment(int ticketId, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return Json(ATMTicketing.Application.Common.ServiceResult.Failure("No file selected."));
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedAttachmentExtensions.Contains(extension))
        {
            return Json(ATMTicketing.Application.Common.ServiceResult.Failure("File type not allowed."));
        }
        if (file.Length > MaxAttachmentBytes)
        {
            return Json(ATMTicketing.Application.Common.ServiceResult.Failure("File exceeds the 10 MB limit."));
        }

        var uploadsRoot = Path.Combine(_environment.WebRootPath, "uploads", "tickets", ticketId.ToString());
        Directory.CreateDirectory(uploadsRoot);

        var safeFileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(uploadsRoot, safeFileName);

        await using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(stream, ct);
        }

        var result = await _ticketService.AddAttachmentAsync(
            ticketId, file.FileName, Path.Combine("uploads", "tickets", ticketId.ToString(), safeFileName), extension, file.Length, ct);

        return Json(result);
    }

    private async Task PopulateDropdownsAsync()
    {
        var categories = await _uow.Categories.GetAllAsync();
        ViewBag.Categories = new SelectList(categories.OrderBy(c => c.Name), "Id", "Name");
    }
}
