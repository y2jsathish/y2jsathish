using ATMTicketing.Application.DTOs;

namespace ATMTicketing.Application.Interfaces.Services;

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken ct = default);
    Task<IReadOnlyList<EngineerWorkloadDto>> GetEngineerWorkloadAsync(CancellationToken ct = default);
    Task<IReadOnlyList<NameValueDto>> GetRegionWiseTicketsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<NameValueDto>> GetPriorityWiseTicketsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<NameValueDto>> GetVendorWiseTicketsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TrendPointDto>> GetDailyTrendAsync(int days, CancellationToken ct = default);
    Task<IReadOnlyList<TrendPointDto>> GetMonthlyTrendAsync(int months, CancellationToken ct = default);
}
