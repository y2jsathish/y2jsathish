using ATMTicketing.Domain.Common;

namespace ATMTicketing.Domain.Entities;

public class Region : BaseEntity
{
    public string RegionName { get; set; } = string.Empty;
    public string Zone { get; set; } = string.Empty;

    public ICollection<Atm> Atms { get; set; } = new List<Atm>();
    public ICollection<Vendor> Vendors { get; set; } = new List<Vendor>();
    public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
}
