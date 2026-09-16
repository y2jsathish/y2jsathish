using ATMTicketing.Application.Interfaces.Services;

namespace ATMTicketing.Tests.TestSupport;

/// <summary>Test double for ITicketNumberGenerator — the production implementation needs a
/// relational database SEQUENCE, which the EF Core InMemory provider can't execute.</summary>
public class InMemoryTicketNumberGenerator : ITicketNumberGenerator
{
    private long _current;

    public Task<long> NextAsync(CancellationToken ct = default) =>
        Task.FromResult(Interlocked.Increment(ref _current));
}
