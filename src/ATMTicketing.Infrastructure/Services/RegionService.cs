using ATMTicketing.Application.Common;
using ATMTicketing.Application.DTOs;
using ATMTicketing.Application.Interfaces.Repositories;
using ATMTicketing.Application.Interfaces.Services;
using ATMTicketing.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ATMTicketing.Infrastructure.Services;

public class RegionService : IRegionService
{
    private readonly IUnitOfWork _uow;
    private readonly UserManager<ApplicationUser> _userManager;

    public RegionService(IUnitOfWork uow, UserManager<ApplicationUser> userManager)
    {
        _uow = uow;
        _userManager = userManager;
    }

    public async Task<IReadOnlyList<RegionListItemDto>> GetAllAsync(CancellationToken ct = default)
    {
        var regions = await _uow.Regions.Query().OrderBy(r => r.RegionName).ToListAsync(ct);
        var regionIds = regions.Select(r => r.Id).ToList();

        var atmCounts = await _uow.Atms.Query()
            .Where(a => regionIds.Contains(a.RegionId))
            .GroupBy(a => a.RegionId)
            .Select(g => new { RegionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.RegionId, g => g.Count, ct);

        var vendorCounts = await _uow.Vendors.Query()
            .Where(v => regionIds.Contains(v.ServiceRegionId))
            .GroupBy(v => v.ServiceRegionId)
            .Select(g => new { RegionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.RegionId, g => g.Count, ct);

        var userCounts = await _userManager.Users
            .Where(u => u.RegionId != null && regionIds.Contains(u.RegionId.Value))
            .GroupBy(u => u.RegionId!.Value)
            .Select(g => new { RegionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.RegionId, g => g.Count, ct);

        return regions.Select(r => new RegionListItemDto
        {
            Id = r.Id,
            RegionName = r.RegionName,
            Zone = r.Zone,
            IsActive = r.IsActive,
            AtmCount = atmCounts.GetValueOrDefault(r.Id, 0),
            VendorCount = vendorCounts.GetValueOrDefault(r.Id, 0),
            UserCount = userCounts.GetValueOrDefault(r.Id, 0)
        }).ToList();
    }

    public async Task<RegionEditDto?> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var region = await _uow.Regions.GetByIdAsync(id, ct);
        if (region is null)
        {
            return null;
        }

        return new RegionEditDto
        {
            Id = region.Id,
            RegionName = region.RegionName,
            Zone = region.Zone,
            IsActive = region.IsActive
        };
    }

    public async Task<ServiceResult> CreateAsync(RegionEditDto dto, CancellationToken ct = default)
    {
        var nameExists = await _uow.Regions.Query().AnyAsync(r => r.RegionName == dto.RegionName, ct);
        if (nameExists)
        {
            return ServiceResult.Failure("A region with this name already exists.");
        }

        await _uow.Regions.AddAsync(new Region
        {
            RegionName = dto.RegionName,
            Zone = dto.Zone,
            IsActive = dto.IsActive
        }, ct);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success("Region created successfully.");
    }

    public async Task<ServiceResult> UpdateAsync(RegionEditDto dto, CancellationToken ct = default)
    {
        var region = await _uow.Regions.Query(asNoTracking: false).FirstOrDefaultAsync(r => r.Id == dto.Id, ct);
        if (region is null)
        {
            return ServiceResult.Failure("Region not found.");
        }

        var nameExists = await _uow.Regions.Query().AnyAsync(r => r.RegionName == dto.RegionName && r.Id != dto.Id, ct);
        if (nameExists)
        {
            return ServiceResult.Failure("Another region already uses this name.");
        }

        region.RegionName = dto.RegionName;
        region.Zone = dto.Zone;
        region.IsActive = dto.IsActive;
        region.UpdatedDate = DateTime.UtcNow;

        _uow.Regions.Update(region);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success("Region updated successfully.");
    }

    public async Task<ServiceResult> DeleteAsync(int id, CancellationToken ct = default)
    {
        var region = await _uow.Regions.GetByIdAsync(id, ct);
        if (region is null)
        {
            return ServiceResult.Failure("Region not found.");
        }

        var hasAtms = await _uow.Atms.Query().AnyAsync(a => a.RegionId == id, ct);
        var hasVendors = await _uow.Vendors.Query().AnyAsync(v => v.ServiceRegionId == id, ct);
        var hasUsers = await _userManager.Users.AnyAsync(u => u.RegionId == id, ct);
        if (hasAtms || hasVendors || hasUsers)
        {
            return ServiceResult.Failure("Cannot delete a region that has ATMs, vendors, or users assigned to it. Deactivate it instead.");
        }

        _uow.Regions.Remove(region);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success("Region deleted.");
    }
}
