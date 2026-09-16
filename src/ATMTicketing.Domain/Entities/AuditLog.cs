namespace ATMTicketing.Domain.Entities;

/// <summary>Generic system-wide audit trail: logins, entity changes, status transitions.</summary>
public class AuditLog
{
    public long Id { get; set; }

    public string? UserId { get; set; }
    public string? UserName { get; set; }

    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string? EntityId { get; set; }

    public string? OldValues { get; set; }
    public string? NewValues { get; set; }

    public string? IpAddress { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
