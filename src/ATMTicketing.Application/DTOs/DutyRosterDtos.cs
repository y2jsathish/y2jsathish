using System.ComponentModel.DataAnnotations;
using ATMTicketing.Domain.Enums;

namespace ATMTicketing.Application.DTOs;

public class DutyRosterListItemDto
{
    public int Id { get; set; }
    public DateOnly DutyDate { get; set; }
    public DutyShift Shift { get; set; }
    public int RegionId { get; set; }
    public string RegionName { get; set; } = string.Empty;
    public string EngineerId { get; set; } = string.Empty;
    public string EngineerName { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class DutyRosterEditDto
{
    public int Id { get; set; }

    [Required]
    public DateOnly DutyDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    [Required]
    public DutyShift Shift { get; set; } = DutyShift.General;

    [Required]
    public int RegionId { get; set; }

    [Required]
    public string EngineerId { get; set; } = string.Empty;

    [StringLength(300)]
    public string? Notes { get; set; }
}
