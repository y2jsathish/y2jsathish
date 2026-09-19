using ATMTicketing.Domain.Common;
using ATMTicketing.Domain.Enums;

namespace ATMTicketing.Domain.Entities;

/// <summary>One field engineer's on-duty coverage for a single date/shift in a region. Auto
/// assignment prefers whoever the roster names on duty for the ticket's region and today's
/// date before falling back to the plain region/workload picker.</summary>
public class DutyRoster : BaseEntity
{
    public DateOnly DutyDate { get; set; }
    public DutyShift Shift { get; set; }
    public int RegionId { get; set; }
    public string EngineerId { get; set; } = string.Empty;
    public string? Notes { get; set; }

    public Region Region { get; set; } = null!;
    public ApplicationUser Engineer { get; set; } = null!;
}
