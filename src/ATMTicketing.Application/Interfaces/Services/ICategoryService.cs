using ATMTicketing.Application.Common;
using ATMTicketing.Application.DTOs;

namespace ATMTicketing.Application.Interfaces.Services;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryListItemDto>> GetAllAsync(CancellationToken ct = default);
    Task<CategoryEditDto?> GetForEditAsync(int id, CancellationToken ct = default);
    Task<ServiceResult> CreateAsync(CategoryEditDto dto, CancellationToken ct = default);
    Task<ServiceResult> UpdateAsync(CategoryEditDto dto, CancellationToken ct = default);
    Task<ServiceResult> DeleteAsync(int id, CancellationToken ct = default);
}
