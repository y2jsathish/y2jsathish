using ATMTicketing.Domain.Enums;

namespace ATMTicketing.Domain.Entities;

public class Notification
{
    public int Id { get; set; }

    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public int? TicketId { get; set; }
    public Ticket? Ticket { get; set; }

    public NotificationChannel Channel { get; set; }
    public NotificationEvent Event { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    public bool IsRead { get; set; }
    public bool IsSent { get; set; }
    public string? ErrorMessage { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? SentDate { get; set; }
}
