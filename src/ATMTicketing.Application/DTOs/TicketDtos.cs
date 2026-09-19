using System.ComponentModel.DataAnnotations;
using ATMTicketing.Domain.Enums;

namespace ATMTicketing.Application.DTOs;

public class TicketCreateDto
{
    [Required]
    public int AtmId { get; set; }

    [Required, StringLength(100)]
    public string IncidentType { get; set; } = string.Empty;

    [Required]
    public int CategoryId { get; set; }

    [StringLength(100)]
    public string? SubCategory { get; set; }

    [Required]
    public PriorityLevel Priority { get; set; }

    [Required, StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string ContactPerson { get; set; } = string.Empty;

    [Required, Phone, StringLength(20)]
    public string ContactNumber { get; set; } = string.Empty;
}

public class TicketEditDto
{
    [Required]
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string IncidentType { get; set; } = string.Empty;

    [Required]
    public int CategoryId { get; set; }

    [StringLength(100)]
    public string? SubCategory { get; set; }

    [Required]
    public PriorityLevel Priority { get; set; }

    [Required, StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string ContactPerson { get; set; } = string.Empty;

    [Required, Phone, StringLength(20)]
    public string ContactNumber { get; set; } = string.Empty;
}

public class TicketListItemDto
{
    public int Id { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string AtmCode { get; set; } = string.Empty;
    public string AtmName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusColor { get; set; } = "#6c757d";
    public string? AssignedToName { get; set; }
    public string RegionName { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTime? ResolutionDueAt { get; set; }
    public bool IsResolutionBreached { get; set; }
    public bool IsEscalated { get; set; }
}

public class TicketDetailsDto
{
    public int Id { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public int AtmId { get; set; }
    public string AtmCode { get; set; } = string.Empty;
    public string AtmName { get; set; } = string.Empty;
    public string AtmAddress { get; set; } = string.Empty;
    public string IncidentType { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? SubCategory { get; set; }
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int StatusId { get; set; }
    public string StatusColor { get; set; } = "#6c757d";
    public string Description { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public string ContactNumber { get; set; } = string.Empty;
    public int RegionId { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string? AssignedToId { get; set; }
    public string? AssignedToName { get; set; }
    public string? VendorName { get; set; }
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

    public List<TicketHistoryDto> History { get; set; } = new();
    public List<TicketAttachmentDto> Attachments { get; set; } = new();
}

public class TicketHistoryDto
{
    public string ActionType { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Notes { get; set; }
    public string ActionByName { get; set; } = string.Empty;
    public DateTime ActionDate { get; set; }
}

public class TicketAttachmentDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string UploadedByName { get; set; } = string.Empty;
    public DateTime UploadedDate { get; set; }
    public string Url { get; set; } = string.Empty;
}

public class TicketStatusUpdateDto
{
    [Required]
    public int TicketId { get; set; }

    [Required]
    public int NewStatusId { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }
}

public class TicketAssignDto
{
    [Required]
    public int TicketId { get; set; }

    [Required]
    public string EngineerId { get; set; } = string.Empty;

    public AssignmentType AssignmentType { get; set; } = AssignmentType.Manual;
}

public class TicketCloseDto
{
    [Required]
    public int TicketId { get; set; }

    [Required, StringLength(2000)]
    public string ResolutionNotes { get; set; } = string.Empty;

    [Required, StringLength(1000)]
    public string ClosureRemarks { get; set; } = string.Empty;
}

public class TicketFilterDto
{
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public int? RegionId { get; set; }
    public int? CategoryId { get; set; }
    public string? AssignedToId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public bool? BreachedOnly { get; set; }
}

/// <summary>A candidate for the manual "Assign / Reassign" dropdown. Lists every active
/// Field Engineer — not just ones who already have a ticket assigned — so a brand-new
/// engineer or a system with no assignments yet still has someone to pick.</summary>
public class EngineerOptionDto
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? RegionName { get; set; }
    public bool IsSameRegionAsTicket { get; set; }
    public int OpenTicketCount { get; set; }
}
