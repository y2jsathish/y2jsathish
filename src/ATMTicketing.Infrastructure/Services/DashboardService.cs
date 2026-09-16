using ATMTicketing.Application.DTOs;
using ATMTicketing.Application.Interfaces.Repositories;
using ATMTicketing.Application.Interfaces.Services;
using ATMTicketing.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ATMTicketing.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private static readonly int[] OpenStatusIds =
    {
        (int)TicketStatusCode.New, (int)TicketStatusCode.Assigned, (int)TicketStatusCode.InProgress,
        (int)TicketStatusCode.PendingParts, (int)TicketStatusCode.PendingCustomer, (int)TicketStatusCode.Escalated
    };

    private readonly IUnitOfWork _uow;

    public DashboardService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var tickets = _uow.Tickets.Query();

        var total = await tickets.CountAsync(ct);
        var open = await tickets.CountAsync(t => OpenStatusIds.Contains(t.StatusId), ct);
        var closed = await tickets.CountAsync(t => t.StatusId == (int)TicketStatusCode.Closed, ct);
        var breaches = await tickets.CountAsync(t => t.IsResolutionBreached, ct);
        var criticalOpen = await tickets.CountAsync(t => OpenStatusIds.Contains(t.StatusId) && t.Priority == PriorityLevel.Critical, ct);
        var atmDown = await _uow.Atms.Query().CountAsync(a => a.Status == Domain.Enums.AtmOperationalStatus.Inactive || a.Status == Domain.Enums.AtmOperationalStatus.UnderMaintenance, ct);

        var closedTotal = await tickets.CountAsync(t => t.StatusId == (int)TicketStatusCode.Closed || t.StatusId == (int)TicketStatusCode.Resolved, ct);
        var compliantClosed = await tickets.CountAsync(t =>
            (t.StatusId == (int)TicketStatusCode.Closed || t.StatusId == (int)TicketStatusCode.Resolved) && !t.IsResolutionBreached, ct);

        return new DashboardSummaryDto
        {
            TotalTickets = total,
            OpenTickets = open,
            ClosedTickets = closed,
            SlaBreaches = breaches,
            AtmDownCount = atmDown,
            CriticalOpenTickets = criticalOpen,
            SlaCompliancePercent = closedTotal == 0 ? 100 : Math.Round(compliantClosed * 100.0 / closedTotal, 1)
        };
    }

    public async Task<IReadOnlyList<EngineerWorkloadDto>> GetEngineerWorkloadAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;

        var grouped = await _uow.Tickets.Query()
            .Where(t => t.AssignedToId != null)
            .GroupBy(t => new { t.AssignedToId, t.AssignedTo!.FullName })
            .Select(g => new EngineerWorkloadDto
            {
                EngineerId = g.Key.AssignedToId!,
                EngineerName = g.Key.FullName,
                OpenTickets = g.Count(t => OpenStatusIds.Contains(t.StatusId)),
                InProgressTickets = g.Count(t => t.StatusId == (int)TicketStatusCode.InProgress),
                ResolvedToday = g.Count(t => t.ResolvedAt != null && t.ResolvedAt.Value.Date == today)
            })
            .OrderByDescending(g => g.OpenTickets)
            .ToListAsync(ct);

        return grouped;
    }

    public async Task<IReadOnlyList<NameValueDto>> GetRegionWiseTicketsAsync(CancellationToken ct = default)
    {
        return await _uow.Tickets.Query()
            .GroupBy(t => t.Atm.Region.RegionName)
            .Select(g => new NameValueDto { Name = g.Key, Value = g.Count() })
            .OrderByDescending(x => x.Value)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<NameValueDto>> GetPriorityWiseTicketsAsync(CancellationToken ct = default)
    {
        var grouped = await _uow.Tickets.Query()
            .Where(t => OpenStatusIds.Contains(t.StatusId))
            .GroupBy(t => t.Priority)
            .Select(g => new { Priority = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return grouped.Select(g => new NameValueDto { Name = g.Priority.ToString(), Value = g.Count }).ToList();
    }

    public async Task<IReadOnlyList<NameValueDto>> GetVendorWiseTicketsAsync(CancellationToken ct = default)
    {
        return await _uow.Tickets.Query()
            .Where(t => t.AssignedVendor != null)
            .GroupBy(t => t.AssignedVendor!.VendorName)
            .Select(g => new NameValueDto { Name = g.Key, Value = g.Count() })
            .OrderByDescending(x => x.Value)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TrendPointDto>> GetDailyTrendAsync(int days, CancellationToken ct = default)
    {
        var from = DateTime.UtcNow.Date.AddDays(-days + 1);
        var created = await _uow.Tickets.Query()
            .Where(t => t.CreatedDate >= from)
            .GroupBy(t => t.CreatedDate.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var resolved = await _uow.Tickets.Query()
            .Where(t => t.ResolvedAt != null && t.ResolvedAt >= from)
            .GroupBy(t => t.ResolvedAt!.Value.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var points = new List<TrendPointDto>();
        for (var day = from; day <= DateTime.UtcNow.Date; day = day.AddDays(1))
        {
            points.Add(new TrendPointDto
            {
                Label = day.ToString("MMM dd"),
                Created = created.FirstOrDefault(c => c.Date == day)?.Count ?? 0,
                Resolved = resolved.FirstOrDefault(r => r.Date == day)?.Count ?? 0
            });
        }
        return points;
    }

    public async Task<IReadOnlyList<TrendPointDto>> GetMonthlyTrendAsync(int months, CancellationToken ct = default)
    {
        var from = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-months + 1);
        var tickets = await _uow.Tickets.Query().Where(t => t.CreatedDate >= from).ToListAsync(ct);

        var points = new List<TrendPointDto>();
        for (var month = from; month <= DateTime.UtcNow; month = month.AddMonths(1))
        {
            points.Add(new TrendPointDto
            {
                Label = month.ToString("MMM yyyy"),
                Created = tickets.Count(t => t.CreatedDate.Year == month.Year && t.CreatedDate.Month == month.Month),
                Resolved = tickets.Count(t => t.ResolvedAt.HasValue && t.ResolvedAt.Value.Year == month.Year && t.ResolvedAt.Value.Month == month.Month)
            });
        }
        return points;
    }
}
