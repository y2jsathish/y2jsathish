using ATMTicketing.Application.DTOs;

namespace ATMTicketing.Application.Interfaces.Services;

public interface IReportService
{
    Task<IReadOnlyList<TicketSummaryReportRow>> GetTicketSummaryAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<IReadOnlyList<TicketAgingReportRow>> GetTicketAgingAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SlaComplianceReportRow>> GetSlaComplianceAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<IReadOnlyList<AtmUptimeReportRow>> GetAtmUptimeAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<IReadOnlyList<EngineerPerformanceReportRow>> GetEngineerPerformanceAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<IReadOnlyList<VendorPerformanceReportRow>> GetVendorPerformanceAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<IReadOnlyList<RegionalAnalysisReportRow>> GetRegionalAnalysisAsync(CancellationToken ct = default);

    byte[] ExportToExcel<T>(IReadOnlyList<T> rows, string sheetName);
    byte[] ExportToPdf<T>(IReadOnlyList<T> rows, string title, IReadOnlyList<(string Header, Func<T, string> Value)> columns);
}
