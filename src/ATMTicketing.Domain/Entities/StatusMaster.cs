using ATMTicketing.Domain.Common;

namespace ATMTicketing.Domain.Entities;

/// <summary>
/// Display/workflow metadata for each TicketStatusCode value. The Id is seeded to match
/// the enum so Ticket.StatusId can be treated as either an FK or a strongly-typed code.
/// </summary>
public class StatusMaster : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string ColorHex { get; set; } = "#6c757d";
    public bool IsTerminal { get; set; }

    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
