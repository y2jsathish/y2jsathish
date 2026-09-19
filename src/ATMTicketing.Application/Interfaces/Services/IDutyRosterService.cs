using ATMTicketing.Application.Common;
using ATMTicketing.Application.DTOs;

namespace ATMTicketing.Application.Interfaces.Services;

/// <summary>Manages the field-engineer duty roster (who covers which region, on which date
/// and shift) that the assignment engine and manual reassign dropdown both consult.</summary>
public interface IDutyRosterService
{
    Task<IReadOnlyList<DutyRosterListItemDto>> GetAsync(DateOnly? fromDate, DateOnly? toDate, int? regionId, CancellationToken ct = default);
    Task<DutyRosterEditDto?> GetForEditAsync(int id, CancellationToken ct = default);
    Task<ServiceResult> CreateAsync(DutyRosterEditDto dto, CancellationToken ct = default);
    Task<ServiceResult> UpdateAsync(DutyRosterEditDto dto, CancellationToken ct = default);
    Task<ServiceResult> DeleteAsync(int id, CancellationToken ct = default);
}
