using ATMTicketing.Domain.Common;
using ATMTicketing.Domain.Enums;

namespace ATMTicketing.Domain.Entities;

/// <summary>One row per PriorityLevel. Admin-editable; drives SlaService's due-date calculation.</summary>
public class SlaConfiguration : BaseEntity
{
    public PriorityLevel Priority { get; set; }
    public int ResponseMinutes { get; set; }
    public int ResolutionMinutes { get; set; }

    /// <summary>Percentage of remaining resolution time at which a breach-warning notification fires.</summary>
    public int WarningThresholdPercent { get; set; } = 80;
}
