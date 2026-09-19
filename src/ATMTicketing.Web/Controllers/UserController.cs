using ATMTicketing.Application.Common;
using ATMTicketing.Application.DTOs;
using ATMTicketing.Application.Interfaces.Repositories;
using ATMTicketing.Domain.Entities;
using ATMTicketing.Web.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ATMTicketing.Web.Controllers;

[Authorize(Roles = Roles.Administrator)]
public class UserController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUnitOfWork _uow;

    public UserController(UserManager<ApplicationUser> userManager, IUnitOfWork uow)
    {
        _userManager = userManager;
        _uow = uow;
    }

    public async Task<IActionResult> Index()
    {
        var users = await _userManager.Users.Include(u => u.Region).OrderBy(u => u.FullName).ToListAsync();
        var items = new List<UserListItemDto>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            items.Add(new UserListItemDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                EmployeeCode = user.EmployeeCode,
                RegionName = user.Region?.RegionName,
                Roles = roles.ToList(),
                IsActive = user.IsActive,
                LastLoginDate = user.LastLoginDate
            });
        }
        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateDropdownsAsync();
        return View(new UserCreateDto());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserCreateDto dto)
    {
        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync();
            return View(dto);
        }

        var user = new ApplicationUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            FullName = dto.FullName,
            EmployeeCode = dto.EmployeeCode,
            RegionId = dto.RegionId,
            EmailConfirmed = true,
            IsActive = true
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            await PopulateDropdownsAsync();
            return View(dto);
        }

        await _userManager.AddToRoleAsync(user, dto.Role);
        TempData["Success"] = "User created successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        var roles = await _userManager.GetRolesAsync(user);
        await PopulateDropdownsAsync();

        return View(new UserEditDto
        {
            Id = user.Id,
            FullName = user.FullName,
            EmployeeCode = user.EmployeeCode,
            RegionId = user.RegionId,
            Role = roles.FirstOrDefault() ?? string.Empty,
            IsActive = user.IsActive
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UserEditDto dto)
    {
        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync();
            return View(dto);
        }

        var user = await _userManager.FindByIdAsync(dto.Id);
        if (user is null)
        {
            return NotFound();
        }

        user.FullName = dto.FullName;
        user.EmployeeCode = dto.EmployeeCode;
        user.RegionId = dto.RegionId;
        user.IsActive = dto.IsActive;
        await _userManager.UpdateAsync(user);

        var currentRoles = await _userManager.GetRolesAsync(user);
        if (!currentRoles.Contains(dto.Role))
        {
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, dto.Role);
        }

        TempData["Success"] = "User updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        user.IsActive = !user.IsActive;
        await _userManager.UpdateAsync(user);
        return Json(ServiceResult.Success(user.IsActive ? "User activated." : "User deactivated."));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        if (id == User.GetUserId())
        {
            return Json(ServiceResult.Failure("You cannot delete your own account."));
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return Json(ServiceResult.Failure("User not found."));
        }

        var hasTicketActivity =
            await _uow.Tickets.Query().AnyAsync(t => t.CreatedById == id || t.AssignedToId == id, ct) ||
            await _uow.TicketHistories.Query().AnyAsync(h => h.ActionById == id, ct) ||
            await _uow.TicketAssignments.Query().AnyAsync(a => a.AssignedToId == id || a.AssignedById == id, ct) ||
            await _uow.TicketAttachments.Query().AnyAsync(a => a.UploadedById == id, ct);

        if (hasTicketActivity)
        {
            return Json(ServiceResult.Failure(
                "Cannot delete a user with ticket history (created, assigned, or logged activity). Deactivate the account instead."));
        }

        var result = await _userManager.DeleteAsync(user);
        return result.Succeeded
            ? Json(ServiceResult.Success("User deleted."))
            : Json(ServiceResult.Failure(result.Errors.Select(e => e.Description).ToArray()));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var temporaryPassword = $"Temp@{Guid.NewGuid().ToString("N")[..8]}";
        var result = await _userManager.ResetPasswordAsync(user, token, temporaryPassword);

        return result.Succeeded
            ? Json(ServiceResult<string>.Success(temporaryPassword, "Temporary password generated."))
            : Json(ServiceResult.Failure(result.Errors.Select(e => e.Description).ToArray()));
    }

    private async Task PopulateDropdownsAsync()
    {
        var regions = await _uow.Regions.GetAllAsync();
        ViewBag.Regions = new SelectList(regions.OrderBy(r => r.RegionName), "Id", "RegionName");
        ViewBag.Roles = new SelectList(ATMTicketing.Domain.Entities.Roles.All);
    }
}
