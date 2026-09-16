using ATMTicketing.Application.Common;
using ATMTicketing.Application.DTOs;
using ATMTicketing.Application.Interfaces.Repositories;
using ATMTicketing.Application.Interfaces.Services;
using ATMTicketing.Domain.Entities;
using ATMTicketing.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ATMTicketing.Infrastructure.Services;

public class TicketService : ITicketService
{
    private readonly IUnitOfWork _uow;
    private readonly ISlaService _slaService;
    private readonly IAssignmentService _assignmentService;
    private readonly INotificationService _notificationService;
    private readonly ICurrentUserService _currentUser;
    private readonly ITicketNumberGenerator _ticketNumberGenerator;

    public TicketService(
        IUnitOfWork uow,
        ISlaService slaService,
        IAssignmentService assignmentService,
        INotificationService notificationService,
        ICurrentUserService currentUser,
        ITicketNumberGenerator ticketNumberGenerator)
    {
        _uow = uow;
        _slaService = slaService;
        _assignmentService = assignmentService;
        _notificationService = notificationService;
        _currentUser = currentUser;
        _ticketNumberGenerator = ticketNumberGenerator;
    }

    public async Task<ServiceResult<TicketDetailsDto>> CreateAsync(TicketCreateDto dto, CancellationToken ct = default)
    {
        if (_currentUser.UserId is null)
        {
            return ServiceResult<TicketDetailsDto>.Failure("No authenticated user.");
        }

        var atm = await _uow.Atms.GetByIdAsync(dto.AtmId, ct);
        if (atm is null)
        {
            return ServiceResult<TicketDetailsDto>.Failure("Selected ATM was not found.");
        }

        var now = DateTime.UtcNow;
        var (responseDue, resolutionDue) = _slaService.CalculateDueDates(dto.Priority, now);
        var sequence = await _ticketNumberGenerator.NextAsync(ct);
        var ticketNumber = $"TCK-{now:yyyyMM}-{sequence:D6}";

        var ticket = new Ticket
        {
            TicketNumber = ticketNumber,
            AtmId = dto.AtmId,
            IncidentType = dto.IncidentType,
            CategoryId = dto.CategoryId,
            SubCategory = dto.SubCategory,
            Priority = dto.Priority,
            StatusId = (int)TicketStatusCode.New,
            Description = dto.Description,
            ContactPerson = dto.ContactPerson,
            ContactNumber = dto.ContactNumber,
            CreatedById = _currentUser.UserId,
            RegionId = atm.RegionId,
            AssignedVendorId = atm.VendorId,
            ResponseDueAt = responseDue,
            ResolutionDueAt = resolutionDue,
            CreatedDate = now
        };

        await _uow.Tickets.AddAsync(ticket, ct);
        await _uow.SaveChangesAsync(ct); // need ticket.Id for history/assignment rows

        await _uow.TicketHistories.AddAsync(new TicketHistory
        {
            TicketId = ticket.Id,
            ActionType = TicketHistoryAction.Created,
            NewValue = ticketNumber,
            Notes = $"Ticket created for ATM {atm.AtmCode} ({dto.IncidentType}).",
            ActionById = _currentUser.UserId,
            ActionDate = now
        }, ct);

        await _assignmentService.AutoAssignAsync(ticket, ct);
        await _uow.SaveChangesAsync(ct);

        await _notificationService.NotifyAsync(NotificationEvent.TicketCreated, ticket, ticket.CreatedById, ct);
        if (ticket.AssignedToId is not null)
        {
            await _notificationService.NotifyAsync(NotificationEvent.TicketAssigned, ticket, ticket.AssignedToId, ct);
        }

        var details = await GetDetailsAsync(ticket.Id, ct);
        return ServiceResult<TicketDetailsDto>.Success(details!, $"Ticket {ticketNumber} created successfully.");
    }

    public async Task<TicketDetailsDto?> GetDetailsAsync(int ticketId, CancellationToken ct = default)
    {
        var ticket = await _uow.Tickets.Query()
            .Include(t => t.Atm)
            .Include(t => t.Category)
            .Include(t => t.Status)
            .Include(t => t.CreatedBy)
            .Include(t => t.AssignedTo)
            .Include(t => t.AssignedVendor)
            .Include(t => t.Attachments).ThenInclude(a => a.UploadedBy)
            .Include(t => t.History).ThenInclude(h => h.ActionBy)
            .FirstOrDefaultAsync(t => t.Id == ticketId, ct);

        if (ticket is null)
        {
            return null;
        }

        return new TicketDetailsDto
        {
            Id = ticket.Id,
            TicketNumber = ticket.TicketNumber,
            AtmId = ticket.AtmId,
            AtmCode = ticket.Atm.AtmCode,
            AtmName = ticket.Atm.AtmName,
            AtmAddress = ticket.Atm.Address,
            IncidentType = ticket.IncidentType,
            Category = ticket.Category.Name,
            SubCategory = ticket.SubCategory,
            Priority = ticket.Priority.ToString(),
            Status = ticket.Status.DisplayName,
            StatusId = ticket.StatusId,
            StatusColor = ticket.Status.ColorHex,
            Description = ticket.Description,
            ContactPerson = ticket.ContactPerson,
            ContactNumber = ticket.ContactNumber,
            CreatedByName = ticket.CreatedBy.FullName,
            CreatedDate = ticket.CreatedDate,
            AssignedToId = ticket.AssignedToId,
            AssignedToName = ticket.AssignedTo?.FullName,
            VendorName = ticket.AssignedVendor?.VendorName,
            ResponseDueAt = ticket.ResponseDueAt,
            ResolutionDueAt = ticket.ResolutionDueAt,
            RespondedAt = ticket.RespondedAt,
            ResolvedAt = ticket.ResolvedAt,
            ClosedAt = ticket.ClosedAt,
            IsResponseBreached = ticket.IsResponseBreached,
            IsResolutionBreached = ticket.IsResolutionBreached,
            IsEscalated = ticket.IsEscalated,
            ResolutionNotes = ticket.ResolutionNotes,
            ClosureRemarks = ticket.ClosureRemarks,
            History = ticket.History
                .OrderByDescending(h => h.ActionDate)
                .Select(h => new TicketHistoryDto
                {
                    ActionType = h.ActionType.ToString(),
                    OldValue = h.OldValue,
                    NewValue = h.NewValue,
                    Notes = h.Notes,
                    ActionByName = h.ActionBy.FullName,
                    ActionDate = h.ActionDate
                }).ToList(),
            Attachments = ticket.Attachments
                .OrderByDescending(a => a.UploadedDate)
                .Select(a => new TicketAttachmentDto
                {
                    Id = a.Id,
                    FileName = a.FileName,
                    FileType = a.FileType,
                    FileSizeBytes = a.FileSizeBytes,
                    UploadedByName = a.UploadedBy.FullName,
                    UploadedDate = a.UploadedDate,
                    Url = $"/uploads/tickets/{ticket.Id}/{a.FileName}"
                }).ToList()
        };
    }

    public async Task<DataTableResponse<TicketListItemDto>> GetPagedAsync(DataTableRequest request, TicketFilterDto filter, CancellationToken ct = default)
    {
        var query = _uow.Tickets.Query()
            .Include(t => t.Atm)
            .Include(t => t.Status)
            .Include(t => t.AssignedTo)
            .AsQueryable();

        var totalCount = await query.CountAsync(ct);

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = query.Where(t => t.Status.Code == filter.Status);
        }
        if (!string.IsNullOrWhiteSpace(filter.Priority) && Enum.TryParse<PriorityLevel>(filter.Priority, out var priority))
        {
            query = query.Where(t => t.Priority == priority);
        }
        if (filter.RegionId.HasValue)
        {
            query = query.Where(t => t.RegionId == filter.RegionId);
        }
        if (filter.CategoryId.HasValue)
        {
            query = query.Where(t => t.CategoryId == filter.CategoryId);
        }
        if (!string.IsNullOrWhiteSpace(filter.AssignedToId))
        {
            query = query.Where(t => t.AssignedToId == filter.AssignedToId);
        }
        if (filter.FromDate.HasValue)
        {
            query = query.Where(t => t.CreatedDate >= filter.FromDate.Value);
        }
        if (filter.ToDate.HasValue)
        {
            query = query.Where(t => t.CreatedDate <= filter.ToDate.Value);
        }
        if (filter.BreachedOnly == true)
        {
            query = query.Where(t => t.IsResolutionBreached);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchValue))
        {
            var term = request.SearchValue.Trim();
            query = query.Where(t =>
                t.TicketNumber.Contains(term) ||
                t.Atm.AtmCode.Contains(term) ||
                t.Atm.AtmName.Contains(term) ||
                t.Description.Contains(term));
        }

        var filteredCount = await query.CountAsync(ct);

        query = (request.SortColumn, request.SortDirection) switch
        {
            ("priority", "asc") => query.OrderBy(t => t.Priority),
            ("priority", _) => query.OrderByDescending(t => t.Priority),
            ("status", "asc") => query.OrderBy(t => t.Status.SortOrder),
            ("status", _) => query.OrderByDescending(t => t.Status.SortOrder),
            (_, "asc") => query.OrderBy(t => t.CreatedDate),
            _ => query.OrderByDescending(t => t.CreatedDate)
        };

        var items = await query
            .Skip(request.Start)
            .Take(request.Length <= 0 ? 25 : request.Length)
            .Select(t => new TicketListItemDto
            {
                Id = t.Id,
                TicketNumber = t.TicketNumber,
                AtmCode = t.Atm.AtmCode,
                AtmName = t.Atm.AtmName,
                Category = t.Category.Name,
                Priority = t.Priority.ToString(),
                Status = t.Status.DisplayName,
                StatusColor = t.Status.ColorHex,
                AssignedToName = t.AssignedTo != null ? t.AssignedTo.FullName : null,
                RegionName = t.Atm.Region.RegionName,
                CreatedDate = t.CreatedDate,
                ResolutionDueAt = t.ResolutionDueAt,
                IsResolutionBreached = t.IsResolutionBreached,
                IsEscalated = t.IsEscalated
            })
            .ToListAsync(ct);

        return new DataTableResponse<TicketListItemDto>
        {
            Draw = request.Draw,
            RecordsTotal = totalCount,
            RecordsFiltered = filteredCount,
            Data = items
        };
    }

    public async Task<ServiceResult> UpdateStatusAsync(TicketStatusUpdateDto dto, CancellationToken ct = default)
    {
        if (_currentUser.UserId is null)
        {
            return ServiceResult.Failure("No authenticated user.");
        }

        var ticket = await _uow.Tickets.Query(asNoTracking: false)
            .Include(t => t.Status)
            .FirstOrDefaultAsync(t => t.Id == dto.TicketId, ct);
        if (ticket is null)
        {
            return ServiceResult.Failure("Ticket not found.");
        }

        var newStatus = await _uow.Statuses.GetByIdAsync(dto.NewStatusId, ct);
        if (newStatus is null)
        {
            return ServiceResult.Failure("Invalid status.");
        }

        var oldStatusName = ticket.Status.DisplayName;
        var now = DateTime.UtcNow;

        if (ticket.RespondedAt is null && newStatus.Id != (int)TicketStatusCode.New)
        {
            ticket.RespondedAt = now;
        }
        if (newStatus.Id == (int)TicketStatusCode.Resolved)
        {
            ticket.ResolvedAt = now;
        }

        ticket.StatusId = newStatus.Id;
        _uow.Tickets.Update(ticket);

        await _uow.TicketHistories.AddAsync(new TicketHistory
        {
            TicketId = ticket.Id,
            ActionType = TicketHistoryAction.StatusChanged,
            OldValue = oldStatusName,
            NewValue = newStatus.DisplayName,
            Notes = dto.Notes,
            ActionById = _currentUser.UserId,
            ActionDate = now
        }, ct);

        await _uow.SaveChangesAsync(ct);

        if (newStatus.Id == (int)TicketStatusCode.Resolved)
        {
            await _notificationService.NotifyAsync(NotificationEvent.TicketResolved, ticket, ticket.CreatedById, ct);
        }

        return ServiceResult.Success("Ticket status updated.");
    }

    public async Task<ServiceResult> AssignAsync(TicketAssignDto dto, CancellationToken ct = default)
    {
        if (_currentUser.UserId is null)
        {
            return ServiceResult.Failure("No authenticated user.");
        }

        var ticket = await _uow.Tickets.Query(asNoTracking: false).FirstOrDefaultAsync(t => t.Id == dto.TicketId, ct);
        if (ticket is null)
        {
            return ServiceResult.Failure("Ticket not found.");
        }

        var previousAssignments = await _uow.TicketAssignments.Query(asNoTracking: false)
            .Where(a => a.TicketId == ticket.Id && a.IsCurrent)
            .ToListAsync(ct);
        foreach (var previous in previousAssignments)
        {
            previous.IsCurrent = false;
            previous.UnassignedDate = DateTime.UtcNow;
            _uow.TicketAssignments.Update(previous);
        }

        var oldAssignee = ticket.AssignedToId;
        ticket.AssignedToId = dto.EngineerId;
        if (ticket.StatusId == (int)TicketStatusCode.New)
        {
            ticket.StatusId = (int)TicketStatusCode.Assigned;
        }
        _uow.Tickets.Update(ticket);

        await _uow.TicketAssignments.AddAsync(new TicketAssignment
        {
            TicketId = ticket.Id,
            AssignedToId = dto.EngineerId,
            AssignedById = _currentUser.UserId,
            AssignmentType = oldAssignee is null ? AssignmentType.Manual : AssignmentType.Reassigned,
            IsCurrent = true
        }, ct);

        await _uow.TicketHistories.AddAsync(new TicketHistory
        {
            TicketId = ticket.Id,
            ActionType = oldAssignee is null ? TicketHistoryAction.Assigned : TicketHistoryAction.Reassigned,
            OldValue = oldAssignee,
            NewValue = dto.EngineerId,
            ActionById = _currentUser.UserId
        }, ct);

        await _uow.SaveChangesAsync(ct);
        await _notificationService.NotifyAsync(NotificationEvent.TicketAssigned, ticket, dto.EngineerId, ct);

        return ServiceResult.Success("Ticket assigned.");
    }

    public async Task<ServiceResult> EscalateAsync(int ticketId, string reason, CancellationToken ct = default)
    {
        if (_currentUser.UserId is null)
        {
            return ServiceResult.Failure("No authenticated user.");
        }

        var ticket = await _uow.Tickets.Query(asNoTracking: false).FirstOrDefaultAsync(t => t.Id == ticketId, ct);
        if (ticket is null)
        {
            return ServiceResult.Failure("Ticket not found.");
        }

        ticket.IsEscalated = true;
        ticket.StatusId = (int)TicketStatusCode.Escalated;
        _uow.Tickets.Update(ticket);

        await _uow.TicketHistories.AddAsync(new TicketHistory
        {
            TicketId = ticket.Id,
            ActionType = TicketHistoryAction.Escalated,
            Notes = reason,
            ActionById = _currentUser.UserId
        }, ct);

        await _uow.SaveChangesAsync(ct);
        await _notificationService.NotifyAsync(NotificationEvent.TicketEscalated, ticket, ct: ct);

        return ServiceResult.Success("Ticket escalated.");
    }

    public async Task<ServiceResult> CloseAsync(TicketCloseDto dto, CancellationToken ct = default)
    {
        if (_currentUser.UserId is null)
        {
            return ServiceResult.Failure("No authenticated user.");
        }

        var ticket = await _uow.Tickets.Query(asNoTracking: false).FirstOrDefaultAsync(t => t.Id == dto.TicketId, ct);
        if (ticket is null)
        {
            return ServiceResult.Failure("Ticket not found.");
        }

        ticket.StatusId = (int)TicketStatusCode.Closed;
        ticket.ResolutionNotes = dto.ResolutionNotes;
        ticket.ClosureRemarks = dto.ClosureRemarks;
        ticket.ClosedAt = DateTime.UtcNow;
        ticket.ResolvedAt ??= ticket.ClosedAt;
        _uow.Tickets.Update(ticket);

        await _uow.TicketHistories.AddAsync(new TicketHistory
        {
            TicketId = ticket.Id,
            ActionType = TicketHistoryAction.Closed,
            Notes = dto.ClosureRemarks,
            ActionById = _currentUser.UserId
        }, ct);

        await _uow.SaveChangesAsync(ct);
        await _notificationService.NotifyAsync(NotificationEvent.TicketClosed, ticket, ticket.CreatedById, ct);

        return ServiceResult.Success("Ticket closed.");
    }

    public async Task<ServiceResult> AddAttachmentAsync(int ticketId, string fileName, string storedPath, string fileType, long sizeBytes, CancellationToken ct = default)
    {
        if (_currentUser.UserId is null)
        {
            return ServiceResult.Failure("No authenticated user.");
        }

        var exists = await _uow.Tickets.Query().AnyAsync(t => t.Id == ticketId, ct);
        if (!exists)
        {
            return ServiceResult.Failure("Ticket not found.");
        }

        await _uow.TicketAttachments.AddAsync(new TicketAttachment
        {
            TicketId = ticketId,
            FileName = fileName,
            StoredFilePath = storedPath,
            FileType = fileType,
            FileSizeBytes = sizeBytes,
            UploadedById = _currentUser.UserId
        }, ct);

        await _uow.TicketHistories.AddAsync(new TicketHistory
        {
            TicketId = ticketId,
            ActionType = TicketHistoryAction.AttachmentAdded,
            NewValue = fileName,
            ActionById = _currentUser.UserId
        }, ct);

        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success("Attachment uploaded.");
    }

    public async Task<IReadOnlyList<TicketListItemDto>> GetMyTicketsAsync(string engineerId, CancellationToken ct = default)
    {
        return await _uow.Tickets.Query()
            .Include(t => t.Atm)
            .Include(t => t.Status)
            .Where(t => t.AssignedToId == engineerId && t.ClosedAt == null)
            .OrderBy(t => t.ResolutionDueAt)
            .Select(t => new TicketListItemDto
            {
                Id = t.Id,
                TicketNumber = t.TicketNumber,
                AtmCode = t.Atm.AtmCode,
                AtmName = t.Atm.AtmName,
                Category = t.Category.Name,
                Priority = t.Priority.ToString(),
                Status = t.Status.DisplayName,
                StatusColor = t.Status.ColorHex,
                RegionName = t.Atm.Region.RegionName,
                CreatedDate = t.CreatedDate,
                ResolutionDueAt = t.ResolutionDueAt,
                IsResolutionBreached = t.IsResolutionBreached,
                IsEscalated = t.IsEscalated
            })
            .ToListAsync(ct);
    }
}
