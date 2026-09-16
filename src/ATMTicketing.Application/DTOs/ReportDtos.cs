namespace ATMTicketing.Application.DTOs;

public class TicketSummaryReportRow
{
    public string TicketNumber { get; set; } = string.Empty;
    public string AtmCode { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string? AssignedTo { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ClosedAt { get; set; }
}

public class TicketAgingReportRow
{
    public string TicketNumber { get; set; } = string.Empty;
    public string AtmCode { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int AgeInHours { get; set; }
    public string? AssignedTo { get; set; }
}

public class SlaComplianceReportRow
{
    public string Priority { get; set; } = string.Empty;
    public int TotalTickets { get; set; }
    public int MetSla { get; set; }
    public int Breached { get; set; }
    public double CompliancePercent { get; set; }
}

public class AtmUptimeReportRow
{
    public string AtmCode { get; set; } = string.Empty;
    public string AtmName { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public int DownCount { get; set; }
    public double TotalDowntimeHours { get; set; }
    public double UptimePercent { get; set; }
}

public class EngineerPerformanceReportRow
{
    public string EngineerName { get; set; } = string.Empty;
    public int TotalAssigned { get; set; }
    public int Resolved { get; set; }
    public double AvgResolutionHours { get; set; }
    public int SlaBreaches { get; set; }
}

public class VendorPerformanceReportRow
{
    public string VendorName { get; set; } = string.Empty;
    public int TotalTickets { get; set; }
    public int Resolved { get; set; }
    public double AvgResolutionHours { get; set; }
    public int SlaBreaches { get; set; }
}

public class RegionalAnalysisReportRow
{
    public string Region { get; set; } = string.Empty;
    public int TotalTickets { get; set; }
    public int OpenTickets { get; set; }
    public int Breaches { get; set; }
    public int AtmCount { get; set; }
}
