using System.ComponentModel.DataAnnotations;
using ATMTicketing.Domain.Enums;

namespace ATMTicketing.Application.DTOs;

public class SlaConfigurationDto
{
    public int Id { get; set; }
    public PriorityLevel Priority { get; set; }

    [Range(1, 1440)]
    public int ResponseMinutes { get; set; }

    [Range(1, 20160)]
    public int ResolutionMinutes { get; set; }

    [Range(1, 100)]
    public int WarningThresholdPercent { get; set; }
}

public class SlaDashboardTicketDto
{
    public int Id { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string AtmCode { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? ResolutionDueAt { get; set; }
    public int MinutesRemaining { get; set; }
    public bool IsBreached { get; set; }
}
