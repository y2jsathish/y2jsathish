using ATMTicketing.Application.Interfaces.Services;
using ATMTicketing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ATMTicketing.Infrastructure.Services;

/// <summary>Default production implementation: reserves the next value of the SQL Server
/// dbo.TicketNumberSequence (see ApplicationDbContext.OnModelCreating / database/01_Schema.sql).</summary>
public class SqlSequenceTicketNumberGenerator : ITicketNumberGenerator
{
    private readonly ApplicationDbContext _context;

    public SqlSequenceTicketNumberGenerator(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<long> NextAsync(CancellationToken ct = default)
    {
        var result = await _context.Database
            .SqlQueryRaw<long>("SELECT NEXT VALUE FOR dbo.TicketNumberSequence AS [Value]")
            .ToListAsync(ct);
        return result[0];
    }
}
