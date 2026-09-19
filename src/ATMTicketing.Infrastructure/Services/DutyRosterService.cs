using ATMTicketing.Application.Common;
using ATMTicketing.Application.DTOs;
using ATMTicketing.Application.Interfaces.Repositories;
using ATMTicketing.Application.Interfaces.Services;
using ATMTicketing.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ATMTicketing.Infrastructure.Services;

public class DutyRosterService : IDutyRosterService
{
    private readonly IUnitOfWork _uow;
    private readonly UserManager<ApplicationUser> _userManager;

    public DutyRosterService(IUnitOfWork uow, UserManager<ApplicationUser> userManager)
    {
        _uow = uow;
        _userManager = userManager;
    }

    public async Task<IReadOnlyList<DutyRosterListItemDto>> GetAsync(DateOnly? fromDate, DateOnly? toDate, int? regionId, CancellationToken ct = default)
    {
        var query = _uow.DutyRosters.Query();
        if (fromDate.HasValue)
        {
            query = query.Where(d => d.DutyDate >= fromDate.Value);
        }
        if (toDate.HasValue)
        {
            query = query.Where(d => d.DutyDate <= toDate.Value);
        }
        if (regionId.HasValue)
        {
            query = query.Where(d => d.RegionId == regionId.Value);
        }

        var rows = await query
            .OrderBy(d => d.DutyDate).ThenBy(d => d.RegionId).ThenBy(d => d.Shift)
            .ToListAsync(ct);

        var regionNames = (await _uow.Regions.GetAllAsync(ct)).ToDictionary(r => r.Id, r => r.RegionName);
        var engineerNames = new Dictionary<string, string>();
        foreach (var engineerId in rows.Select(r => r.EngineerId).Distinct())
        {
            var engineer = await _userManager.FindByIdAsync(engineerId);
            engineerNames[engineerId] = engineer?.FullName ?? "Unknown";
        }

        return rows.Select(d => new DutyRosterListItemDto
        {
            Id = d.Id,
            DutyDate = d.DutyDate,
            Shift = d.Shift,
            RegionId = d.RegionId,
            RegionName = regionNames.GetValueOrDefault(d.RegionId, "-"),
            EngineerId = d.EngineerId,
            EngineerName = engineerNames.GetValueOrDefault(d.EngineerId, "Unknown"),
            Notes = d.Notes
        }).ToList();
    }

    public async Task<DutyRosterEditDto?> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var roster = await _uow.DutyRosters.GetByIdAsync(id, ct);
        if (roster is null)
        {
            return null;
        }

        return new DutyRosterEditDto
        {
            Id = roster.Id,
            DutyDate = roster.DutyDate,
            Shift = roster.Shift,
            RegionId = roster.RegionId,
            EngineerId = roster.EngineerId,
            Notes = roster.Notes
        };
    }

    public async Task<ServiceResult> CreateAsync(DutyRosterEditDto dto, CancellationToken ct = default)
    {
        var validation = await ValidateAsync(dto, ct);
        if (validation is not null)
        {
            return validation;
        }

        await _uow.DutyRosters.AddAsync(new DutyRoster
        {
            DutyDate = dto.DutyDate,
            Shift = dto.Shift,
            RegionId = dto.RegionId,
            EngineerId = dto.EngineerId,
            Notes = dto.Notes
        }, ct);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success("Duty roster entry added.");
    }

    public async Task<ServiceResult> UpdateAsync(DutyRosterEditDto dto, CancellationToken ct = default)
    {
        var roster = await _uow.DutyRosters.Query(asNoTracking: false).FirstOrDefaultAsync(d => d.Id == dto.Id, ct);
        if (roster is null)
        {
            return ServiceResult.Failure("Duty roster entry not found.");
        }

        var validation = await ValidateAsync(dto, ct);
        if (validation is not null)
        {
            return validation;
        }

        roster.DutyDate = dto.DutyDate;
        roster.Shift = dto.Shift;
        roster.RegionId = dto.RegionId;
        roster.EngineerId = dto.EngineerId;
        roster.Notes = dto.Notes;
        roster.UpdatedDate = DateTime.UtcNow;

        _uow.DutyRosters.Update(roster);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success("Duty roster entry updated.");
    }

    public async Task<ServiceResult> DeleteAsync(int id, CancellationToken ct = default)
    {
        var roster = await _uow.DutyRosters.GetByIdAsync(id, ct);
        if (roster is null)
        {
            return ServiceResult.Failure("Duty roster entry not found.");
        }

        _uow.DutyRosters.Remove(roster);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success("Duty roster entry removed.");
    }

    private async Task<ServiceResult?> ValidateAsync(DutyRosterEditDto dto, CancellationToken ct)
    {
        var engineer = await _userManager.FindByIdAsync(dto.EngineerId);
        if (engineer is null || !await _userManager.IsInRoleAsync(engineer, Roles.FieldEngineer))
        {
            return ServiceResult.Failure("Select a valid Field Engineer.");
        }

        var regionExists = await _uow.Regions.Query().AnyAsync(r => r.Id == dto.RegionId, ct);
        if (!regionExists)
        {
            return ServiceResult.Failure("Select a valid region.");
        }

        var duplicate = await _uow.DutyRosters.Query()
            .AnyAsync(d => d.Id != dto.Id && d.EngineerId == dto.EngineerId && d.DutyDate == dto.DutyDate && d.Shift == dto.Shift, ct);
        if (duplicate)
        {
            return ServiceResult.Failure("This engineer is already rostered for that date and shift.");
        }

        return null;
    }
}
