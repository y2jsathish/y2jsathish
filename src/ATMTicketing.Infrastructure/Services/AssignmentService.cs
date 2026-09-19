using ATMTicketing.Application.DTOs;
using ATMTicketing.Application.Interfaces.Repositories;
using ATMTicketing.Application.Interfaces.Services;
using ATMTicketing.Domain.Entities;
using ATMTicketing.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ATMTicketing.Infrastructure.Services;

/// <summary>
/// Picks the field engineer for a new ticket using, in order: same region, active account,
/// lowest current open-ticket workload. Falls back to leaving the ticket unassigned (status
/// stays New) so a Team Lead can triage manually when no engineer covers the region.
/// </summary>
public class AssignmentService : IAssignmentService
{
    private static readonly int[] OpenStatusIds =
    {
        (int)TicketStatusCode.Assigned,
        (int)TicketStatusCode.InProgress,
        (int)TicketStatusCode.PendingParts,
        (int)TicketStatusCode.PendingCustomer
    };

    private readonly IUnitOfWork _uow;
    private readonly UserManager<ApplicationUser> _userManager;

    public AssignmentService(IUnitOfWork uow, UserManager<ApplicationUser> userManager)
    {
        _uow = uow;
        _userManager = userManager;
    }

    public async Task<ApplicationUser?> FindBestEngineerAsync(Ticket ticket, CancellationToken ct = default)
    {
        var engineerRoleUsers = await _userManager.GetUsersInRoleAsync(Roles.FieldEngineer);
        var onDutyIds = await GetOnDutyEngineerIdsAsync(ticket.RegionId, ct);

        // The duty roster is authoritative when it names anyone on duty for this region today;
        // only fall back to the plain region/workload picker when nobody is rostered on.
        var candidateIds = onDutyIds.Count > 0
            ? engineerRoleUsers.Where(u => u.IsActive && onDutyIds.Contains(u.Id)).Select(u => u.Id).ToList()
            : engineerRoleUsers.Where(u => u.IsActive && u.RegionId == ticket.RegionId).Select(u => u.Id).ToList();

        // Region-based match found none -> widen to any active engineer (vendor/zone coverage gaps happen).
        if (candidateIds.Count == 0)
        {
            candidateIds = engineerRoleUsers.Where(u => u.IsActive).Select(u => u.Id).ToList();
        }

        if (candidateIds.Count == 0)
        {
            return null;
        }

        var workloads = await _uow.Tickets.Query()
            .Where(t => t.AssignedToId != null && candidateIds.Contains(t.AssignedToId!) && OpenStatusIds.Contains(t.StatusId))
            .GroupBy(t => t.AssignedToId!)
            .Select(g => new { EngineerId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.EngineerId, g => g.Count, ct);

        var bestId = candidateIds
            .OrderBy(id => workloads.GetValueOrDefault(id, 0))
            .First();

        return engineerRoleUsers.First(u => u.Id == bestId);
    }

    public async Task<bool> AutoAssignAsync(Ticket ticket, CancellationToken ct = default)
    {
        var engineer = await FindBestEngineerAsync(ticket, ct);
        if (engineer is null)
        {
            return false;
        }

        ticket.AssignedToId = engineer.Id;
        ticket.StatusId = (int)TicketStatusCode.Assigned;

        await _uow.TicketAssignments.AddAsync(new TicketAssignment
        {
            TicketId = ticket.Id,
            AssignedToId = engineer.Id,
            AssignedById = ticket.CreatedById,
            AssignmentType = AssignmentType.Auto,
            IsCurrent = true
        }, ct);

        await _uow.TicketHistories.AddAsync(new TicketHistory
        {
            TicketId = ticket.Id,
            ActionType = TicketHistoryAction.Assigned,
            NewValue = engineer.FullName,
            Notes = "Auto-assigned by the assignment engine (region + workload based).",
            ActionById = ticket.CreatedById
        }, ct);

        return true;
    }

    public async Task<IReadOnlyList<EngineerOptionDto>> GetAssignableEngineersAsync(int? ticketRegionId, CancellationToken ct = default)
    {
        var engineers = (await _userManager.GetUsersInRoleAsync(Roles.FieldEngineer))
            .Where(u => u.IsActive)
            .ToList();

        if (engineers.Count == 0)
        {
            return Array.Empty<EngineerOptionDto>();
        }

        var engineerIds = engineers.Select(e => e.Id).ToList();
        var workloads = await _uow.Tickets.Query()
            .Where(t => t.AssignedToId != null && engineerIds.Contains(t.AssignedToId!) && OpenStatusIds.Contains(t.StatusId))
            .GroupBy(t => t.AssignedToId!)
            .Select(g => new { EngineerId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.EngineerId, g => g.Count, ct);

        var regionNames = (await _uow.Regions.GetAllAsync(ct)).ToDictionary(r => r.Id, r => r.RegionName);
        var onDutyIds = ticketRegionId.HasValue
            ? await GetOnDutyEngineerIdsAsync(ticketRegionId.Value, ct)
            : Array.Empty<string>();

        return engineers
            .Select(e => new EngineerOptionDto
            {
                Id = e.Id,
                FullName = e.FullName,
                RegionName = e.RegionId.HasValue ? regionNames.GetValueOrDefault(e.RegionId.Value) : null,
                IsSameRegionAsTicket = ticketRegionId.HasValue && e.RegionId == ticketRegionId,
                OpenTicketCount = workloads.GetValueOrDefault(e.Id, 0),
                IsOnDuty = onDutyIds.Contains(e.Id)
            })
            .OrderByDescending(e => e.IsOnDuty)
            .ThenByDescending(e => e.IsSameRegionAsTicket)
            .ThenBy(e => e.OpenTicketCount)
            .ThenBy(e => e.FullName)
            .ToList();
    }

    /// <summary>Field Engineers the duty roster names on duty for <paramref name="regionId"/>
    /// today (UTC date). Empty when nobody is rostered on, in which case callers fall back to
    /// their own region/workload logic rather than treating an empty roster as "nobody works".</summary>
    private async Task<IReadOnlyCollection<string>> GetOnDutyEngineerIdsAsync(int regionId, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await _uow.DutyRosters.Query()
            .Where(d => d.RegionId == regionId && d.DutyDate == today)
            .Select(d => d.EngineerId)
            .Distinct()
            .ToListAsync(ct);
    }
}
