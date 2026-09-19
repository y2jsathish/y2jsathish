using ATMTicketing.Application.Common;
using ATMTicketing.Application.DTOs;

namespace ATMTicketing.Application.Interfaces.Services;

public interface IRegionService
{
    Task<IReadOnlyList<RegionListItemDto>> GetAllAsync(CancellationToken ct = default);
    Task<RegionEditDto?> GetForEditAsync(int id, CancellationToken ct = default);
    Task<ServiceResult> CreateAsync(RegionEditDto dto, CancellationToken ct = default);
    Task<ServiceResult> UpdateAsync(RegionEditDto dto, CancellationToken ct = default);
    Task<ServiceResult> DeleteAsync(int id, CancellationToken ct = default);
}
