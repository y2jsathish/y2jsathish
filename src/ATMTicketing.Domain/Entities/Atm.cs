using ATMTicketing.Domain.Common;
using ATMTicketing.Domain.Enums;

namespace ATMTicketing.Domain.Entities;

public class Atm : BaseEntity
{
    /// <summary>Business ATM code (e.g. bank's terminal ID), distinct from the surrogate Id.</summary>
    public string AtmCode { get; set; } = string.Empty;
    public string AtmName { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public int RegionId { get; set; }
    public Region Region { get; set; } = null!;
    public string Zone { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int VendorId { get; set; }
    public Vendor Vendor { get; set; } = null!;
    public AtmType AtmType { get; set; }
    public AtmOperationalStatus Status { get; set; } = AtmOperationalStatus.Active;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
