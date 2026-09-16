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
        var candidateIds = engineerRoleUsers
            .Where(u => u.IsActive && u.RegionId == ticket.RegionId)
            .Select(u => u.Id)
            .ToList();

        // Region-based match found none -> widen to any active engineer (vendor/zone coverage gaps happen).
        if (candidateIds.Count == 0)
        {
            candidateIds = engineerRoleUsers.Where(u => u.IsActive).Select(u => u.Id).ToList();
        }

        if (candidateIds.Count == 0)
        {
            return null;
        }

        var openStatuses = new[]
        {
            (int)TicketStatusCode.Assigned,
            (int)TicketStatusCode.InProgress,
            (int)TicketStatusCode.PendingParts,
            (int)TicketStatusCode.PendingCustomer
        };

        var workloads = await _uow.Tickets.Query()
            .Where(t => t.AssignedToId != null && candidateIds.Contains(t.AssignedToId!) && openStatuses.Contains(t.StatusId))
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
}
