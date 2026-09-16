using ATMTicketing.Domain.Enums;

namespace ATMTicketing.Domain.Entities;

/// <summary>Immutable audit-style trail of every change made to a ticket (the "timeline").</summary>
public class TicketHistory
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;

    public TicketHistoryAction ActionType { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Notes { get; set; }

    public string ActionById { get; set; } = string.Empty;
    public ApplicationUser ActionBy { get; set; } = null!;
    public DateTime ActionDate { get; set; } = DateTime.UtcNow;
}
