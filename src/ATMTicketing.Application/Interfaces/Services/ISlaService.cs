using ATMTicketing.Application.Common;
using ATMTicketing.Application.DTOs;
using ATMTicketing.Domain.Entities;
using ATMTicketing.Domain.Enums;

namespace ATMTicketing.Application.Interfaces.Services;

public interface ISlaService
{
    (DateTime responseDueAt, DateTime resolutionDueAt) CalculateDueDates(PriorityLevel priority, DateTime fromUtc);
    Task<IReadOnlyList<SlaConfigurationDto>> GetConfigurationsAsync(CancellationToken ct = default);
    Task<ServiceResult> UpdateConfigurationAsync(SlaConfigurationDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<SlaDashboardTicketDto>> GetSlaDashboardAsync(CancellationToken ct = default);

    /// <summary>Scans open tickets for breach/warning thresholds; invoked by the SLA background monitor.</summary>
    Task<int> EvaluateOpenTicketsAsync(CancellationToken ct = default);
}
