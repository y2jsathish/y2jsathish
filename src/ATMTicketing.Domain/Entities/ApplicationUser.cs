using Microsoft.AspNetCore.Identity;

namespace ATMTicketing.Domain.Entities;

/// <summary>
/// Identity user extended with the profile fields the ticketing workflow needs
/// (region scoping for auto-assignment, display name, activation state).
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public string? EmployeeCode { get; set; }
    public int? RegionId { get; set; }
    public Region? Region { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginDate { get; set; }

    public ICollection<Ticket> CreatedTickets { get; set; } = new List<Ticket>();
    public ICollection<Ticket> AssignedTickets { get; set; } = new List<Ticket>();
}
