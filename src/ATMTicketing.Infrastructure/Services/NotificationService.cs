using System.Net;
using System.Net.Mail;
using ATMTicketing.Application.Interfaces.Repositories;
using ATMTicketing.Application.Interfaces.Services;
using ATMTicketing.Domain.Entities;
using ATMTicketing.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ATMTicketing.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly IUnitOfWork _uow;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IUnitOfWork uow,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ILogger<NotificationService> logger)
    {
        _uow = uow;
        _userManager = userManager;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task NotifyAsync(NotificationEvent @event, Ticket ticket, string? userId = null, CancellationToken ct = default)
    {
        var targetUserId = userId ?? ticket.AssignedToId ?? ticket.CreatedById;
        var (subject, message) = BuildContent(@event, ticket);

        var notification = new Notification
        {
            UserId = targetUserId,
            TicketId = ticket.Id,
            Channel = NotificationChannel.InApp,
            Event = @event,
            Subject = subject,
            Message = message
        };

        await _uow.Notifications.AddAsync(notification, ct);
        await _uow.SaveChangesAsync(ct);

        await TrySendEmailAsync(targetUserId, subject, message, ct);
    }

    public async Task<IReadOnlyList<Notification>> GetUnreadForUserAsync(string userId, CancellationToken ct = default)
    {
        return await _uow.Notifications.Query()
            .Where(n => n.UserId == userId && !n.IsRead)
            .OrderByDescending(n => n.CreatedDate)
            .Take(50)
            .ToListAsync(ct);
    }

    public async Task MarkAsReadAsync(int notificationId, CancellationToken ct = default)
    {
        var entity = await _uow.Notifications.GetByIdAsync(notificationId, ct);
        if (entity is null)
        {
            return;
        }

        entity.IsRead = true;
        _uow.Notifications.Update(entity);
        await _uow.SaveChangesAsync(ct);
    }

    private static (string subject, string message) BuildContent(NotificationEvent @event, Ticket ticket) => @event switch
    {
        NotificationEvent.TicketCreated => ($"Ticket {ticket.TicketNumber} created", $"A new {ticket.Priority} priority ticket has been logged for ATM {ticket.AtmId}."),
        NotificationEvent.TicketAssigned => ($"Ticket {ticket.TicketNumber} assigned to you", $"You have been assigned ticket {ticket.TicketNumber}."),
        NotificationEvent.TicketEscalated => ($"Ticket {ticket.TicketNumber} escalated", $"Ticket {ticket.TicketNumber} has been escalated and needs attention."),
        NotificationEvent.TicketResolved => ($"Ticket {ticket.TicketNumber} resolved", $"Ticket {ticket.TicketNumber} has been marked resolved."),
        NotificationEvent.TicketClosed => ($"Ticket {ticket.TicketNumber} closed", $"Ticket {ticket.TicketNumber} has been closed."),
        NotificationEvent.SlaBreachWarning => ($"SLA warning: {ticket.TicketNumber}", $"Ticket {ticket.TicketNumber} is approaching its SLA resolution deadline."),
        NotificationEvent.SlaBreached => ($"SLA BREACHED: {ticket.TicketNumber}", $"Ticket {ticket.TicketNumber} has breached its SLA resolution deadline."),
        _ => ($"Ticket {ticket.TicketNumber} update", "Ticket status has changed.")
    };

    private async Task TrySendEmailAsync(string? userId, string subject, string message, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        var smtpHost = _configuration["Smtp:Host"];
        if (string.IsNullOrWhiteSpace(smtpHost))
        {
            return; // Email disabled/not configured — in-app notification already recorded.
        }

        try
        {
            var recipient = await _userManager.FindByIdAsync(userId);
            if (string.IsNullOrWhiteSpace(recipient?.Email))
            {
                return;
            }

            using var client = new SmtpClient(smtpHost, int.Parse(_configuration["Smtp:Port"] ?? "587"))
            {
                Credentials = new NetworkCredential(_configuration["Smtp:User"], _configuration["Smtp:Password"]),
                EnableSsl = bool.Parse(_configuration["Smtp:EnableSsl"] ?? "true")
            };

            using var mail = new MailMessage
            {
                From = new MailAddress(_configuration["Smtp:FromAddress"] ?? "noreply@atmticketing.local", "ATM Ticketing System"),
                Subject = subject,
                Body = message,
                IsBodyHtml = false
            };
            mail.To.Add(recipient.Email);

            // Best-effort delivery: SMTP failures are logged, never propagated, so ticket workflow never blocks on email.
            await client.SendMailAsync(mail, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send SMTP email notification for user {UserId}", userId);
        }
    }
}
