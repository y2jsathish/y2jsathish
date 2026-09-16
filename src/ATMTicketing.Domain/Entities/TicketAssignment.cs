using ATMTicketing.Domain.Enums;

namespace ATMTicketing.Domain.Entities;

/// <summary>Full assignment history for a ticket; the row with IsCurrent = true is the active engineer.</summary>
public class TicketAssignment
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;

    public string AssignedToId { get; set; } = string.Empty;
    public ApplicationUser AssignedTo { get; set; } = null!;

    public string AssignedById { get; set; } = string.Empty;
    public ApplicationUser AssignedBy { get; set; } = null!;

    public DateTime AssignedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UnassignedDate { get; set; }
    public AssignmentType AssignmentType { get; set; }
    public bool IsCurrent { get; set; } = true;
}
