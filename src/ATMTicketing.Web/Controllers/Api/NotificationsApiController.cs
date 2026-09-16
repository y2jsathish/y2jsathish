using ATMTicketing.Application.Interfaces.Services;
using ATMTicketing.Web.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ATMTicketing.Web.Controllers.Api;

[Authorize]
[ApiController]
[Route("api/notifications")]
public class NotificationsApiController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsApiController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet("unread")]
    public async Task<IActionResult> Unread(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }
        var notifications = await _notificationService.GetUnreadForUserAsync(userId, ct);
        return Ok(notifications.Select(n => new
        {
            n.Id,
            n.Subject,
            n.Message,
            n.TicketId,
            n.CreatedDate
        }));
    }

    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> MarkAsRead(int id, CancellationToken ct)
    {
        await _notificationService.MarkAsReadAsync(id, ct);
        return NoContent();
    }
}
