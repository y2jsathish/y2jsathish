namespace ATMTicketing.Application.DTOs;

public class DashboardSummaryDto
{
    public int TotalTickets { get; set; }
    public int OpenTickets { get; set; }
    public int ClosedTickets { get; set; }
    public int SlaBreaches { get; set; }
    public int AtmDownCount { get; set; }
    public int CriticalOpenTickets { get; set; }
    public double SlaCompliancePercent { get; set; }
}

public class EngineerWorkloadDto
{
    public string EngineerId { get; set; } = string.Empty;
    public string EngineerName { get; set; } = string.Empty;
    public int OpenTickets { get; set; }
    public int InProgressTickets { get; set; }
    public int ResolvedToday { get; set; }
}

public class NameValueDto
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
}

public class TrendPointDto
{
    public string Label { get; set; } = string.Empty;
    public int Created { get; set; }
    public int Resolved { get; set; }
}
