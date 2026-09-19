using ATMTicketing.Application.Common;
using ATMTicketing.Application.DTOs;

namespace ATMTicketing.Application.Interfaces.Services;

public interface ITicketService
{
    Task<ServiceResult<TicketDetailsDto>> CreateAsync(TicketCreateDto dto, CancellationToken ct = default);
    Task<TicketDetailsDto?> GetDetailsAsync(int ticketId, CancellationToken ct = default);
    Task<TicketEditDto?> GetForEditAsync(int ticketId, CancellationToken ct = default);
    Task<ServiceResult> UpdateAsync(TicketEditDto dto, CancellationToken ct = default);
    Task<DataTableResponse<TicketListItemDto>> GetPagedAsync(DataTableRequest request, TicketFilterDto filter, CancellationToken ct = default);
    Task<ServiceResult> UpdateStatusAsync(TicketStatusUpdateDto dto, CancellationToken ct = default);
    Task<ServiceResult> AssignAsync(TicketAssignDto dto, CancellationToken ct = default);
    Task<ServiceResult> EscalateAsync(int ticketId, string reason, CancellationToken ct = default);
    Task<ServiceResult> CloseAsync(TicketCloseDto dto, CancellationToken ct = default);
    Task<ServiceResult> AddAttachmentAsync(int ticketId, string fileName, string storedPath, string fileType, long sizeBytes, CancellationToken ct = default);
    Task<IReadOnlyList<TicketListItemDto>> GetMyTicketsAsync(string engineerId, CancellationToken ct = default);
}
