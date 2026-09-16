using ATMTicketing.Domain.Entities;

namespace ATMTicketing.Application.Interfaces.Repositories;

public interface IUnitOfWork : IDisposable
{
    IGenericRepository<Atm> Atms { get; }
    IGenericRepository<Vendor> Vendors { get; }
    IGenericRepository<Region> Regions { get; }
    IGenericRepository<Ticket> Tickets { get; }
    IGenericRepository<TicketHistory> TicketHistories { get; }
    IGenericRepository<TicketAssignment> TicketAssignments { get; }
    IGenericRepository<TicketAttachment> TicketAttachments { get; }
    IGenericRepository<SlaConfiguration> SlaConfigurations { get; }
    IGenericRepository<CategoryMaster> Categories { get; }
    IGenericRepository<StatusMaster> Statuses { get; }
    IGenericRepository<Notification> Notifications { get; }
    IGenericRepository<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
