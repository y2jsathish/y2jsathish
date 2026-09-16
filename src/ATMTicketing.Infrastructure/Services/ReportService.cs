using ATMTicketing.Application.DTOs;
using ATMTicketing.Application.Interfaces.Repositories;
using ATMTicketing.Application.Interfaces.Services;
using ATMTicketing.Domain.Enums;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ATMTicketing.Infrastructure.Services;

public class ReportService : IReportService
{
    private readonly IUnitOfWork _uow;

    public ReportService(IUnitOfWork uow)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        _uow = uow;
    }

    public async Task<IReadOnlyList<TicketSummaryReportRow>> GetTicketSummaryAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        return await _uow.Tickets.Query()
            .Where(t => t.CreatedDate >= from && t.CreatedDate <= to)
            .OrderByDescending(t => t.CreatedDate)
            .Select(t => new TicketSummaryReportRow
            {
                TicketNumber = t.TicketNumber,
                AtmCode = t.Atm.AtmCode,
                Category = t.Category.Name,
                Priority = t.Priority.ToString(),
                Status = t.Status.DisplayName,
                Region = t.Atm.Region.RegionName,
                AssignedTo = t.AssignedTo != null ? t.AssignedTo.FullName : null,
                CreatedDate = t.CreatedDate,
                ClosedAt = t.ClosedAt
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TicketAgingReportRow>> GetTicketAgingAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var openStatuses = new[]
        {
            (int)TicketStatusCode.New, (int)TicketStatusCode.Assigned, (int)TicketStatusCode.InProgress,
            (int)TicketStatusCode.PendingParts, (int)TicketStatusCode.PendingCustomer, (int)TicketStatusCode.Escalated
        };

        var tickets = await _uow.Tickets.Query()
            .Where(t => openStatuses.Contains(t.StatusId))
            .Select(t => new
            {
                t.TicketNumber,
                AtmCode = t.Atm.AtmCode,
                Priority = t.Priority.ToString(),
                Status = t.Status.DisplayName,
                t.CreatedDate,
                AssignedTo = t.AssignedTo != null ? t.AssignedTo.FullName : null
            })
            .ToListAsync(ct);

        return tickets
            .Select(t => new TicketAgingReportRow
            {
                TicketNumber = t.TicketNumber,
                AtmCode = t.AtmCode,
                Priority = t.Priority,
                Status = t.Status,
                AgeInHours = (int)(now - t.CreatedDate).TotalHours,
                AssignedTo = t.AssignedTo
            })
            .OrderByDescending(t => t.AgeInHours)
            .ToList();
    }

    public async Task<IReadOnlyList<SlaComplianceReportRow>> GetSlaComplianceAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var closedStatuses = new[] { (int)TicketStatusCode.Closed, (int)TicketStatusCode.Resolved };
        var tickets = await _uow.Tickets.Query()
            .Where(t => closedStatuses.Contains(t.StatusId) && t.CreatedDate >= from && t.CreatedDate <= to)
            .Select(t => new { t.Priority, t.IsResolutionBreached })
            .ToListAsync(ct);

        return tickets
            .GroupBy(t => t.Priority)
            .Select(g => new SlaComplianceReportRow
            {
                Priority = g.Key.ToString(),
                TotalTickets = g.Count(),
                MetSla = g.Count(t => !t.IsResolutionBreached),
                Breached = g.Count(t => t.IsResolutionBreached),
                CompliancePercent = g.Count() == 0 ? 100 : Math.Round(g.Count(t => !t.IsResolutionBreached) * 100.0 / g.Count(), 1)
            })
            .OrderBy(r => r.Priority)
            .ToList();
    }

    public async Task<IReadOnlyList<AtmUptimeReportRow>> GetAtmUptimeAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var totalWindowHours = (to - from).TotalHours;
        var downCategoryId = await _uow.Categories.Query().Where(c => c.Name == "ATM Down").Select(c => c.Id).FirstOrDefaultAsync(ct);

        var atms = await _uow.Atms.Query().Include(a => a.Region).ToListAsync(ct);
        var downTickets = await _uow.Tickets.Query()
            .Where(t => t.CategoryId == downCategoryId && t.CreatedDate >= from && t.CreatedDate <= to)
            .Select(t => new { t.AtmId, t.CreatedDate, t.ResolvedAt, t.ClosedAt })
            .ToListAsync(ct);

        var rows = new List<AtmUptimeReportRow>();
        foreach (var atm in atms)
        {
            var relevant = downTickets.Where(t => t.AtmId == atm.Id).ToList();
            var downtimeHours = relevant.Sum(t => ((t.ClosedAt ?? t.ResolvedAt ?? DateTime.UtcNow) - t.CreatedDate).TotalHours);
            var uptimePercent = totalWindowHours <= 0 ? 100 : Math.Max(0, Math.Round(100 - downtimeHours / totalWindowHours * 100, 2));

            rows.Add(new AtmUptimeReportRow
            {
                AtmCode = atm.AtmCode,
                AtmName = atm.AtmName,
                Region = atm.Region.RegionName,
                DownCount = relevant.Count,
                TotalDowntimeHours = Math.Round(downtimeHours, 1),
                UptimePercent = uptimePercent
            });
        }

        return rows.OrderBy(r => r.UptimePercent).ToList();
    }

    public async Task<IReadOnlyList<EngineerPerformanceReportRow>> GetEngineerPerformanceAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var tickets = await _uow.Tickets.Query()
            .Where(t => t.AssignedToId != null && t.CreatedDate >= from && t.CreatedDate <= to)
            .Select(t => new
            {
                Engineer = t.AssignedTo!.FullName,
                t.ResolvedAt,
                t.CreatedDate,
                t.IsResolutionBreached
            })
            .ToListAsync(ct);

        return tickets
            .GroupBy(t => t.Engineer)
            .Select(g => new EngineerPerformanceReportRow
            {
                EngineerName = g.Key,
                TotalAssigned = g.Count(),
                Resolved = g.Count(t => t.ResolvedAt.HasValue),
                AvgResolutionHours = g.Any(t => t.ResolvedAt.HasValue)
                    ? Math.Round(g.Where(t => t.ResolvedAt.HasValue).Average(t => (t.ResolvedAt!.Value - t.CreatedDate).TotalHours), 1)
                    : 0,
                SlaBreaches = g.Count(t => t.IsResolutionBreached)
            })
            .OrderByDescending(r => r.Resolved)
            .ToList();
    }

    public async Task<IReadOnlyList<VendorPerformanceReportRow>> GetVendorPerformanceAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var tickets = await _uow.Tickets.Query()
            .Where(t => t.AssignedVendor != null && t.CreatedDate >= from && t.CreatedDate <= to)
            .Select(t => new
            {
                Vendor = t.AssignedVendor!.VendorName,
                t.ResolvedAt,
                t.CreatedDate,
                t.IsResolutionBreached
            })
            .ToListAsync(ct);

        return tickets
            .GroupBy(t => t.Vendor)
            .Select(g => new VendorPerformanceReportRow
            {
                VendorName = g.Key,
                TotalTickets = g.Count(),
                Resolved = g.Count(t => t.ResolvedAt.HasValue),
                AvgResolutionHours = g.Any(t => t.ResolvedAt.HasValue)
                    ? Math.Round(g.Where(t => t.ResolvedAt.HasValue).Average(t => (t.ResolvedAt!.Value - t.CreatedDate).TotalHours), 1)
                    : 0,
                SlaBreaches = g.Count(t => t.IsResolutionBreached)
            })
            .OrderByDescending(r => r.TotalTickets)
            .ToList();
    }

    public async Task<IReadOnlyList<RegionalAnalysisReportRow>> GetRegionalAnalysisAsync(CancellationToken ct = default)
    {
        var openStatuses = new[]
        {
            (int)TicketStatusCode.New, (int)TicketStatusCode.Assigned, (int)TicketStatusCode.InProgress,
            (int)TicketStatusCode.PendingParts, (int)TicketStatusCode.PendingCustomer, (int)TicketStatusCode.Escalated
        };

        var regions = await _uow.Regions.GetAllAsync(ct);
        var rows = new List<RegionalAnalysisReportRow>();

        foreach (var region in regions)
        {
            var total = await _uow.Tickets.Query().CountAsync(t => t.RegionId == region.Id, ct);
            var open = await _uow.Tickets.Query().CountAsync(t => t.RegionId == region.Id && openStatuses.Contains(t.StatusId), ct);
            var breaches = await _uow.Tickets.Query().CountAsync(t => t.RegionId == region.Id && t.IsResolutionBreached, ct);
            var atmCount = await _uow.Atms.Query().CountAsync(a => a.RegionId == region.Id, ct);

            rows.Add(new RegionalAnalysisReportRow
            {
                Region = region.RegionName,
                TotalTickets = total,
                OpenTickets = open,
                Breaches = breaches,
                AtmCount = atmCount
            });
        }

        return rows.OrderByDescending(r => r.TotalTickets).ToList();
    }

    public byte[] ExportToExcel<T>(IReadOnlyList<T> rows, string sheetName)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(sheetName);
        var properties = typeof(T).GetProperties();

        for (var col = 0; col < properties.Length; col++)
        {
            worksheet.Cell(1, col + 1).Value = properties[col].Name;
            worksheet.Cell(1, col + 1).Style.Font.Bold = true;
            worksheet.Cell(1, col + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#0B3D91");
            worksheet.Cell(1, col + 1).Style.Font.FontColor = XLColor.White;
        }

        for (var row = 0; row < rows.Count; row++)
        {
            for (var col = 0; col < properties.Length; col++)
            {
                var value = properties[col].GetValue(rows[row]);
                worksheet.Cell(row + 2, col + 1).Value = value?.ToString() ?? string.Empty;
            }
        }

        worksheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] ExportToPdf<T>(IReadOnlyList<T> rows, string title, IReadOnlyList<(string Header, Func<T, string> Value)> columns)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(30);
                page.Header().Text(title).FontSize(16).Bold().FontColor(Colors.Blue.Darken3);

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        foreach (var _ in columns)
                        {
                            cols.RelativeColumn();
                        }
                    });

                    foreach (var column in columns)
                    {
                        table.Cell().Background(Colors.Blue.Darken3).Padding(4)
                            .Text(column.Header).FontColor(Colors.White).Bold();
                    }

                    foreach (var row in rows)
                    {
                        foreach (var column in columns)
                        {
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4)
                                .Text(column.Value(row));
                        }
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Generated ").FontSize(9);
                    x.Span(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm") + " UTC").FontSize(9);
                });
            });
        });

        return document.GeneratePdf();
    }
}
