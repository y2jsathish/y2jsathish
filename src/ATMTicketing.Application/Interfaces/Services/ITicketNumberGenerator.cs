namespace ATMTicketing.Application.Interfaces.Services;

/// <summary>Reserves the next value of a monotonically increasing, gap-tolerant sequence
/// used to build ticket numbers (TCK-yyyyMM-######). Kept separate from IUnitOfWork since
/// it isn't a repository concern and its default implementation needs a relational
/// database (a raw SEQUENCE query) that a test double can swap out for a plain counter.</summary>
public interface ITicketNumberGenerator
{
    Task<long> NextAsync(CancellationToken ct = default);
}
