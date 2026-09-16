using ATMTicketing.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ATMTicketing.Web.Filters;

/// <summary>
/// Lightweight audit hook: records every successful state-changing (POST/PUT/DELETE) MVC action
/// so "Ticket Changes" / "Data Modifications" show up in the Audit Trail module without every
/// controller action having to call IAuditService explicitly.
/// </summary>
public class AuditActionFilter : IAsyncActionFilter
{
    private static readonly HashSet<string> MutatingMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "POST", "PUT", "DELETE", "PATCH"
    };

    private readonly IAuditService _auditService;

    public AuditActionFilter(IAuditService auditService)
    {
        _auditService = auditService;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();

        if (!MutatingMethods.Contains(context.HttpContext.Request.Method))
        {
            return;
        }
        if (executed.Exception is not null && !executed.ExceptionHandled)
        {
            return;
        }
        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var controller = context.RouteData.Values["controller"]?.ToString() ?? "Unknown";
        var action = context.RouteData.Values["action"]?.ToString() ?? "Unknown";
        var entityId = context.RouteData.Values["id"]?.ToString();

        await _auditService.LogAsync($"{controller}.{action}", controller, entityId, null, null);
    }
}
