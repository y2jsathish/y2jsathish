using ATMTicketing.Application.Common;
using ATMTicketing.Application.DTOs;

namespace ATMTicketing.Application.Interfaces.Services;

public interface IAtmService
{
    Task<DataTableResponse<AtmListItemDto>> GetPagedAsync(DataTableRequest request, CancellationToken ct = default);
    Task<AtmEditDto?> GetForEditAsync(int id, CancellationToken ct = default);
    Task<ServiceResult> CreateAsync(AtmEditDto dto, CancellationToken ct = default);
    Task<ServiceResult> UpdateAsync(AtmEditDto dto, CancellationToken ct = default);
    Task<ServiceResult> DeleteAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<AtmListItemDto>> SearchAsync(string term, CancellationToken ct = default);
    Task<IReadOnlyList<AtmImportRowResult>> ImportFromExcelAsync(Stream fileStream, CancellationToken ct = default);
}
