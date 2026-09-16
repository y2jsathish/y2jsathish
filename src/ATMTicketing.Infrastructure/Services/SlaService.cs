using ATMTicketing.Application.Common;
using ATMTicketing.Application.DTOs;
using ATMTicketing.Application.Interfaces.Repositories;
using ATMTicketing.Application.Interfaces.Services;
using ATMTicketing.Domain.Entities;
using ATMTicketing.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ATMTicketing.Infrastructure.Services;

public class SlaService : ISlaService
{
    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notificationService;

    public SlaService(IUnitOfWork uow, INotificationService notificationService)
    {
        _uow = uow;
        _notificationService = notificationService;
    }

    public (DateTime responseDueAt, DateTime resolutionDueAt) CalculateDueDates(PriorityLevel priority, DateTime fromUtc)
    {
        var config = _uow.SlaConfigurations.Query().FirstOrDefault(c => c.Priority == priority)
            ?? throw new InvalidOperationException($"No SLA configuration found for priority '{priority}'.");

        return (fromUtc.AddMinutes(config.ResponseMinutes), fromUtc.AddMinutes(config.ResolutionMinutes));
    }

    public async Task<IReadOnlyList<SlaConfigurationDto>> GetConfigurationsAsync(CancellationToken ct = default)
    {
        var configs = await _uow.SlaConfigurations.Query().OrderBy(c => c.Priority).ToListAsync(ct);
        return configs.Select(c => new SlaConfigurationDto
        {
            Id = c.Id,
            Priority = c.Priority,
            ResponseMinutes = c.ResponseMinutes,
            ResolutionMinutes = c.ResolutionMinutes,
            WarningThresholdPercent = c.WarningThresholdPercent
        }).ToList();
    }

    public async Task<ServiceResult> UpdateConfigurationAsync(SlaConfigurationDto dto, CancellationToken ct = default)
    {
        var entity = await _uow.SlaConfigurations.GetByIdAsync(dto.Id, ct);
        if (entity is null)
        {
            return ServiceResult.Failure("SLA configuration not found.");
        }

        entity.ResponseMinutes = dto.ResponseMinutes;
        entity.ResolutionMinutes = dto.ResolutionMinutes;
        entity.WarningThresholdPercent = dto.WarningThresholdPercent;
        entity.UpdatedDate = DateTime.UtcNow;

        _uow.SlaConfigurations.Update(entity);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success("SLA configuration updated.");
    }

    public async Task<IReadOnlyList<SlaDashboardTicketDto>> GetSlaDashboardAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var openTickets = await _uow.Tickets.Query()
            .Where(t => t.ResolvedAt == null && t.ClosedAt == null)
            .OrderBy(t => t.ResolutionDueAt)
            .Select(t => new SlaDashboardTicketDto
            {
                Id = t.Id,
                TicketNumber = t.TicketNumber,
                AtmCode = t.Atm.AtmCode,
                Priority = t.Priority.ToString(),
                Status = t.Status.DisplayName,
                ResolutionDueAt = t.ResolutionDueAt,
                IsBreached = t.IsResolutionBreached
            })
            .ToListAsync(ct);

        foreach (var t in openTickets)
        {
            t.MinutesRemaining = t.ResolutionDueAt.HasValue
                ? (int)(t.ResolutionDueAt.Value - now).TotalMinutes
                : 0;
        }

        return openTickets;
    }

    public async Task<int> EvaluateOpenTicketsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var openTickets = await _uow.Tickets.Query(asNoTracking: false)
            .Where(t => t.ResolvedAt == null && t.ClosedAt == null)
            .ToListAsync(ct);

        var configs = (await _uow.SlaConfigurations.GetAllAsync(ct)).ToDictionary(c => c.Priority);
        var changed = 0;

        foreach (var ticket in openTickets)
        {
            if (!configs.TryGetValue(ticket.Priority, out var config))
            {
                continue;
            }

            if (ticket.ResolutionDueAt.HasValue && now >= ticket.ResolutionDueAt.Value && !ticket.IsResolutionBreached)
            {
                ticket.IsResolutionBreached = true;
                changed++;
                await _notificationService.NotifyAsync(NotificationEvent.SlaBreached, ticket, ct: ct);
            }
            else if (ticket.ResolutionDueAt.HasValue)
            {
                var totalMinutes = config.ResolutionMinutes;
                var elapsedMinutes = totalMinutes - (ticket.ResolutionDueAt.Value - now).TotalMinutes;
                var percentElapsed = totalMinutes == 0 ? 0 : elapsedMinutes / totalMinutes * 100;
                if (percentElapsed >= config.WarningThresholdPercent && !ticket.IsResolutionBreached)
                {
                    await _notificationService.NotifyAsync(NotificationEvent.SlaBreachWarning, ticket, ct: ct);
                }
            }

            if (ticket.ResponseDueAt.HasValue && now >= ticket.ResponseDueAt.Value && ticket.RespondedAt == null && !ticket.IsResponseBreached)
            {
                ticket.IsResponseBreached = true;
                changed++;
            }
        }

        if (changed > 0)
        {
            await _uow.SaveChangesAsync(ct);
        }

        return changed;
    }
}
