using ATMTicketing.Application.DTOs;
using ATMTicketing.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ATMTicketing.Web.Controllers;

[Authorize(Policy = "CanViewReports")]
public class ReportController : Controller
{
    private readonly IReportService _reportService;

    public ReportController(IReportService reportService)
    {
        _reportService = reportService;
    }

    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> TicketSummary(DateTime? from, DateTime? to, CancellationToken ct)
    {
        var (f, t) = NormalizeRange(from, to);
        var rows = await _reportService.GetTicketSummaryAsync(f, t, ct);
        return View(rows);
    }

    [HttpGet]
    public async Task<IActionResult> TicketAging(CancellationToken ct)
    {
        var rows = await _reportService.GetTicketAgingAsync(ct);
        return View(rows);
    }

    [HttpGet]
    public async Task<IActionResult> SlaCompliance(DateTime? from, DateTime? to, CancellationToken ct)
    {
        var (f, t) = NormalizeRange(from, to);
        var rows = await _reportService.GetSlaComplianceAsync(f, t, ct);
        return View(rows);
    }

    [HttpGet]
    public async Task<IActionResult> AtmUptime(DateTime? from, DateTime? to, CancellationToken ct)
    {
        var (f, t) = NormalizeRange(from, to);
        var rows = await _reportService.GetAtmUptimeAsync(f, t, ct);
        return View(rows);
    }

    [HttpGet]
    public async Task<IActionResult> EngineerPerformance(DateTime? from, DateTime? to, CancellationToken ct)
    {
        var (f, t) = NormalizeRange(from, to);
        var rows = await _reportService.GetEngineerPerformanceAsync(f, t, ct);
        return View(rows);
    }

    [HttpGet]
    public async Task<IActionResult> VendorPerformance(DateTime? from, DateTime? to, CancellationToken ct)
    {
        var (f, t) = NormalizeRange(from, to);
        var rows = await _reportService.GetVendorPerformanceAsync(f, t, ct);
        return View(rows);
    }

    [HttpGet]
    public async Task<IActionResult> RegionalAnalysis(CancellationToken ct)
    {
        var rows = await _reportService.GetRegionalAnalysisAsync(ct);
        return View(rows);
    }

    [HttpGet]
    public async Task<IActionResult> ExportTicketSummary(DateTime? from, DateTime? to, string format, CancellationToken ct)
    {
        var (f, t) = NormalizeRange(from, to);
        var rows = await _reportService.GetTicketSummaryAsync(f, t, ct);

        if (format == "pdf")
        {
            var columns = new (string, Func<TicketSummaryReportRow, string>)[]
            {
                ("Ticket #", r => r.TicketNumber), ("ATM", r => r.AtmCode), ("Category", r => r.Category),
                ("Priority", r => r.Priority), ("Status", r => r.Status), ("Region", r => r.Region),
                ("Assigned To", r => r.AssignedTo ?? "-"), ("Created", r => r.CreatedDate.ToString("yyyy-MM-dd"))
            };
            var pdf = _reportService.ExportToPdf(rows, "Ticket Summary Report", columns);
            return File(pdf, "application/pdf", "TicketSummaryReport.pdf");
        }

        var excel = _reportService.ExportToExcel(rows, "TicketSummary");
        return File(excel, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "TicketSummaryReport.xlsx");
    }

    [HttpGet]
    public async Task<IActionResult> ExportSlaCompliance(DateTime? from, DateTime? to, string format, CancellationToken ct)
    {
        var (f, t) = NormalizeRange(from, to);
        var rows = await _reportService.GetSlaComplianceAsync(f, t, ct);

        if (format == "pdf")
        {
            var columns = new (string, Func<SlaComplianceReportRow, string>)[]
            {
                ("Priority", r => r.Priority), ("Total", r => r.TotalTickets.ToString()),
                ("Met SLA", r => r.MetSla.ToString()), ("Breached", r => r.Breached.ToString()),
                ("Compliance %", r => r.CompliancePercent.ToString("0.0"))
            };
            var pdf = _reportService.ExportToPdf(rows, "SLA Compliance Report", columns);
            return File(pdf, "application/pdf", "SlaComplianceReport.pdf");
        }

        var excel = _reportService.ExportToExcel(rows, "SlaCompliance");
        return File(excel, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "SlaComplianceReport.xlsx");
    }

    [HttpGet]
    public async Task<IActionResult> ExportAtmUptime(DateTime? from, DateTime? to, string format, CancellationToken ct)
    {
        var (f, t) = NormalizeRange(from, to);
        var rows = await _reportService.GetAtmUptimeAsync(f, t, ct);

        if (format == "pdf")
        {
            var columns = new (string, Func<AtmUptimeReportRow, string>)[]
            {
                ("ATM", r => r.AtmCode), ("Name", r => r.AtmName), ("Region", r => r.Region),
                ("Down Count", r => r.DownCount.ToString()), ("Downtime (hrs)", r => r.TotalDowntimeHours.ToString("0.0")),
                ("Uptime %", r => r.UptimePercent.ToString("0.00"))
            };
            var pdf = _reportService.ExportToPdf(rows, "ATM Uptime Report", columns);
            return File(pdf, "application/pdf", "AtmUptimeReport.pdf");
        }

        var excel = _reportService.ExportToExcel(rows, "AtmUptime");
        return File(excel, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "AtmUptimeReport.xlsx");
    }

    [HttpGet]
    public async Task<IActionResult> ExportTicketAging(string format, CancellationToken ct)
    {
        var rows = await _reportService.GetTicketAgingAsync(ct);

        if (format == "pdf")
        {
            var columns = new (string, Func<TicketAgingReportRow, string>)[]
            {
                ("Ticket #", r => r.TicketNumber), ("ATM", r => r.AtmCode), ("Priority", r => r.Priority),
                ("Status", r => r.Status), ("Age (hrs)", r => r.AgeInHours.ToString()), ("Assigned To", r => r.AssignedTo ?? "-")
            };
            var pdf = _reportService.ExportToPdf(rows, "Ticket Aging Report", columns);
            return File(pdf, "application/pdf", "TicketAgingReport.pdf");
        }

        var excel = _reportService.ExportToExcel(rows, "TicketAging");
        return File(excel, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "TicketAgingReport.xlsx");
    }

    [HttpGet]
    public async Task<IActionResult> ExportEngineerPerformance(DateTime? from, DateTime? to, string format, CancellationToken ct)
    {
        var (f, t) = NormalizeRange(from, to);
        var rows = await _reportService.GetEngineerPerformanceAsync(f, t, ct);

        if (format == "pdf")
        {
            var columns = new (string, Func<EngineerPerformanceReportRow, string>)[]
            {
                ("Engineer", r => r.EngineerName), ("Assigned", r => r.TotalAssigned.ToString()),
                ("Resolved", r => r.Resolved.ToString()), ("Avg. Resolution (hrs)", r => r.AvgResolutionHours.ToString("0.0")),
                ("SLA Breaches", r => r.SlaBreaches.ToString())
            };
            var pdf = _reportService.ExportToPdf(rows, "Engineer Performance Report", columns);
            return File(pdf, "application/pdf", "EngineerPerformanceReport.pdf");
        }

        var excel = _reportService.ExportToExcel(rows, "EngineerPerformance");
        return File(excel, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "EngineerPerformanceReport.xlsx");
    }

    [HttpGet]
    public async Task<IActionResult> ExportVendorPerformance(DateTime? from, DateTime? to, string format, CancellationToken ct)
    {
        var (f, t) = NormalizeRange(from, to);
        var rows = await _reportService.GetVendorPerformanceAsync(f, t, ct);

        if (format == "pdf")
        {
            var columns = new (string, Func<VendorPerformanceReportRow, string>)[]
            {
                ("Vendor", r => r.VendorName), ("Total Tickets", r => r.TotalTickets.ToString()),
                ("Resolved", r => r.Resolved.ToString()), ("Avg. Resolution (hrs)", r => r.AvgResolutionHours.ToString("0.0")),
                ("SLA Breaches", r => r.SlaBreaches.ToString())
            };
            var pdf = _reportService.ExportToPdf(rows, "Vendor Performance Report", columns);
            return File(pdf, "application/pdf", "VendorPerformanceReport.pdf");
        }

        var excel = _reportService.ExportToExcel(rows, "VendorPerformance");
        return File(excel, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "VendorPerformanceReport.xlsx");
    }

    [HttpGet]
    public async Task<IActionResult> ExportRegionalAnalysis(string format, CancellationToken ct)
    {
        var rows = await _reportService.GetRegionalAnalysisAsync(ct);

        if (format == "pdf")
        {
            var columns = new (string, Func<RegionalAnalysisReportRow, string>)[]
            {
                ("Region", r => r.Region), ("Total Tickets", r => r.TotalTickets.ToString()),
                ("Open Tickets", r => r.OpenTickets.ToString()), ("SLA Breaches", r => r.Breaches.ToString()),
                ("ATM Count", r => r.AtmCount.ToString())
            };
            var pdf = _reportService.ExportToPdf(rows, "Regional Analysis Report", columns);
            return File(pdf, "application/pdf", "RegionalAnalysisReport.pdf");
        }

        var excel = _reportService.ExportToExcel(rows, "RegionalAnalysis");
        return File(excel, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "RegionalAnalysisReport.xlsx");
    }

    private static (DateTime from, DateTime to) NormalizeRange(DateTime? from, DateTime? to)
    {
        var effectiveTo = (to ?? DateTime.UtcNow).Date.AddDays(1).AddTicks(-1);
        var effectiveFrom = (from ?? effectiveTo.AddDays(-30)).Date;
        return (effectiveFrom, effectiveTo);
    }
}
