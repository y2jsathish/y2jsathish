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
}
