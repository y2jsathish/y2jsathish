using ATMTicketing.Application.DTOs;
using ATMTicketing.Domain.Entities;

namespace ATMTicketing.Application.Interfaces.Services;

/// <summary>
/// Automatic engineer selection: region -> zone -> vendor -> availability -> workload,
/// falling back to leaving the ticket unassigned (New) for manual triage when no match is found.
/// </summary>
public interface IAssignmentService
{
    Task<ApplicationUser?> FindBestEngineerAsync(Ticket ticket, CancellationToken ct = default);
    Task<bool> AutoAssignAsync(Ticket ticket, CancellationToken ct = default);

    /// <summary>Every active Field Engineer with their current open-ticket count, for the
    /// manual Assign/Reassign dropdown — unlike the auto-assignment picker, this must include
    /// engineers with zero tickets so far, not just ones who already have an assignment.</summary>
    Task<IReadOnlyList<EngineerOptionDto>> GetAssignableEngineersAsync(int? ticketRegionId, CancellationToken ct = default);
}
