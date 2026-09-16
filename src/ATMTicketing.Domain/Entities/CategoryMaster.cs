using ATMTicketing.Domain.Common;

namespace ATMTicketing.Domain.Entities;

public class CategoryMaster : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
