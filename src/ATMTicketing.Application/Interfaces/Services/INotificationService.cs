using ATMTicketing.Domain.Entities;
using ATMTicketing.Domain.Enums;

namespace ATMTicketing.Application.Interfaces.Services;

public interface INotificationService
{
    Task NotifyAsync(NotificationEvent @event, Ticket ticket, string? userId = null, CancellationToken ct = default);
    Task<IReadOnlyList<Notification>> GetUnreadForUserAsync(string userId, CancellationToken ct = default);
    Task MarkAsReadAsync(int notificationId, CancellationToken ct = default);
}
