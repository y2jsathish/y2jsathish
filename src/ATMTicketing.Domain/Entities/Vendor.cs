using ATMTicketing.Domain.Common;

namespace ATMTicketing.Domain.Entities;

public class Vendor : BaseEntity
{
    public string VendorCode { get; set; } = string.Empty;
    public string VendorName { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public string ContactNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int ServiceRegionId { get; set; }
    public Region ServiceRegion { get; set; } = null!;

    public ICollection<Atm> Atms { get; set; } = new List<Atm>();
    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
