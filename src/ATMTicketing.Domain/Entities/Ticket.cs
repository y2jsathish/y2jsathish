using ATMTicketing.Domain.Common;
using ATMTicketing.Domain.Enums;

namespace ATMTicketing.Domain.Entities;

public class Ticket : BaseEntity
{
    /// <summary>Auto-generated, human readable ticket number, e.g. TCK-2026-000123.</summary>
    public string TicketNumber { get; set; } = string.Empty;

    public int AtmId { get; set; }
    public Atm Atm { get; set; } = null!;

    public string IncidentType { get; set; } = string.Empty;

    public int CategoryId { get; set; }
    public CategoryMaster Category { get; set; } = null!;
    public string? SubCategory { get; set; }

    public PriorityLevel Priority { get; set; }

    public int StatusId { get; set; }
    public StatusMaster Status { get; set; } = null!;

    public string Description { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public string ContactNumber { get; set; } = string.Empty;

    public string CreatedById { get; set; } = string.Empty;
    public ApplicationUser CreatedBy { get; set; } = null!;

    public string? AssignedToId { get; set; }
    public ApplicationUser? AssignedTo { get; set; }

    public int? AssignedVendorId { get; set; }
    public Vendor? AssignedVendor { get; set; }

    /// <summary>Denormalized from Atm.RegionId at creation time to keep reporting queries index-friendly.</summary>
    public int RegionId { get; set; }

    // SLA timers, computed by SlaService when the ticket is created / (re)prioritized.
    public DateTime? ResponseDueAt { get; set; }
    public DateTime? ResolutionDueAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public bool IsResponseBreached { get; set; }
    public bool IsResolutionBreached { get; set; }
    public bool IsEscalated { get; set; }

    public string? ResolutionNotes { get; set; }
    public string? ClosureRemarks { get; set; }

    public ICollection<TicketHistory> History { get; set; } = new List<TicketHistory>();
    public ICollection<TicketAssignment> Assignments { get; set; } = new List<TicketAssignment>();
    public ICollection<TicketAttachment> Attachments { get; set; } = new List<TicketAttachment>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
