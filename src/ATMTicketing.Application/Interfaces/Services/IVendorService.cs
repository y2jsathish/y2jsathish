using ATMTicketing.Application.Common;
using ATMTicketing.Application.DTOs;

namespace ATMTicketing.Application.Interfaces.Services;

public interface IVendorService
{
    Task<DataTableResponse<VendorListItemDto>> GetPagedAsync(DataTableRequest request, CancellationToken ct = default);
    Task<VendorEditDto?> GetForEditAsync(int id, CancellationToken ct = default);
    Task<ServiceResult> CreateAsync(VendorEditDto dto, CancellationToken ct = default);
    Task<ServiceResult> UpdateAsync(VendorEditDto dto, CancellationToken ct = default);
    Task<ServiceResult> DeleteAsync(int id, CancellationToken ct = default);
}
