using ATMTicketing.Application.Common;
using ATMTicketing.Application.DTOs;
using ATMTicketing.Application.Interfaces.Repositories;
using ATMTicketing.Application.Interfaces.Services;
using ATMTicketing.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ATMTicketing.Infrastructure.Services;

public class CategoryService : ICategoryService
{
    private readonly IUnitOfWork _uow;

    public CategoryService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<IReadOnlyList<CategoryListItemDto>> GetAllAsync(CancellationToken ct = default)
    {
        var categories = await _uow.Categories.Query().OrderBy(c => c.Name).ToListAsync(ct);
        var ticketCounts = await _uow.Tickets.Query()
            .GroupBy(t => t.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.CategoryId, g => g.Count, ct);

        return categories.Select(c => new CategoryListItemDto
        {
            Id = c.Id,
            Name = c.Name,
            Description = c.Description,
            IsActive = c.IsActive,
            TicketCount = ticketCounts.GetValueOrDefault(c.Id, 0)
        }).ToList();
    }

    public async Task<CategoryEditDto?> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var category = await _uow.Categories.GetByIdAsync(id, ct);
        if (category is null)
        {
            return null;
        }

        return new CategoryEditDto
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            IsActive = category.IsActive
        };
    }

    public async Task<ServiceResult> CreateAsync(CategoryEditDto dto, CancellationToken ct = default)
    {
        var nameExists = await _uow.Categories.Query().AnyAsync(c => c.Name == dto.Name, ct);
        if (nameExists)
        {
            return ServiceResult.Failure("A category with this name already exists.");
        }

        await _uow.Categories.AddAsync(new CategoryMaster
        {
            Name = dto.Name,
            Description = dto.Description,
            IsActive = dto.IsActive
        }, ct);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success("Category created successfully.");
    }

    public async Task<ServiceResult> UpdateAsync(CategoryEditDto dto, CancellationToken ct = default)
    {
        var category = await _uow.Categories.Query(asNoTracking: false).FirstOrDefaultAsync(c => c.Id == dto.Id, ct);
        if (category is null)
        {
            return ServiceResult.Failure("Category not found.");
        }

        var nameExists = await _uow.Categories.Query().AnyAsync(c => c.Name == dto.Name && c.Id != dto.Id, ct);
        if (nameExists)
        {
            return ServiceResult.Failure("Another category already uses this name.");
        }

        category.Name = dto.Name;
        category.Description = dto.Description;
        category.IsActive = dto.IsActive;
        category.UpdatedDate = DateTime.UtcNow;

        _uow.Categories.Update(category);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success("Category updated successfully.");
    }

    public async Task<ServiceResult> DeleteAsync(int id, CancellationToken ct = default)
    {
        var category = await _uow.Categories.GetByIdAsync(id, ct);
        if (category is null)
        {
            return ServiceResult.Failure("Category not found.");
        }

        var hasTickets = await _uow.Tickets.Query().AnyAsync(t => t.CategoryId == id, ct);
        if (hasTickets)
        {
            return ServiceResult.Failure("Cannot delete a category that has ticket history. Deactivate it instead.");
        }

        _uow.Categories.Remove(category);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success("Category deleted.");
    }
}
