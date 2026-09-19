using ATMTicketing.Application.Interfaces.Repositories;
using ATMTicketing.Domain.Entities;

namespace ATMTicketing.Infrastructure.Persistence.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;

    private IGenericRepository<Atm>? _atms;
    private IGenericRepository<Vendor>? _vendors;
    private IGenericRepository<Region>? _regions;
    private IGenericRepository<Ticket>? _tickets;
    private IGenericRepository<TicketHistory>? _ticketHistories;
    private IGenericRepository<TicketAssignment>? _ticketAssignments;
    private IGenericRepository<TicketAttachment>? _ticketAttachments;
    private IGenericRepository<SlaConfiguration>? _slaConfigurations;
    private IGenericRepository<CategoryMaster>? _categories;
    private IGenericRepository<StatusMaster>? _statuses;
    private IGenericRepository<Notification>? _notifications;
    private IGenericRepository<AuditLog>? _auditLogs;
    private IGenericRepository<DutyRoster>? _dutyRosters;

    public UnitOfWork(ApplicationDbContext context)
    {
        _context = context;
    }

    public IGenericRepository<Atm> Atms => _atms ??= new GenericRepository<Atm>(_context);
    public IGenericRepository<Vendor> Vendors => _vendors ??= new GenericRepository<Vendor>(_context);
    public IGenericRepository<Region> Regions => _regions ??= new GenericRepository<Region>(_context);
    public IGenericRepository<Ticket> Tickets => _tickets ??= new GenericRepository<Ticket>(_context);
    public IGenericRepository<TicketHistory> TicketHistories => _ticketHistories ??= new GenericRepository<TicketHistory>(_context);
    public IGenericRepository<TicketAssignment> TicketAssignments => _ticketAssignments ??= new GenericRepository<TicketAssignment>(_context);
    public IGenericRepository<TicketAttachment> TicketAttachments => _ticketAttachments ??= new GenericRepository<TicketAttachment>(_context);
    public IGenericRepository<SlaConfiguration> SlaConfigurations => _slaConfigurations ??= new GenericRepository<SlaConfiguration>(_context);
    public IGenericRepository<CategoryMaster> Categories => _categories ??= new GenericRepository<CategoryMaster>(_context);
    public IGenericRepository<StatusMaster> Statuses => _statuses ??= new GenericRepository<StatusMaster>(_context);
    public IGenericRepository<Notification> Notifications => _notifications ??= new GenericRepository<Notification>(_context);
    public IGenericRepository<AuditLog> AuditLogs => _auditLogs ??= new GenericRepository<AuditLog>(_context);
    public IGenericRepository<DutyRoster> DutyRosters => _dutyRosters ??= new GenericRepository<DutyRoster>(_context);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _context.SaveChangesAsync(ct);

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
