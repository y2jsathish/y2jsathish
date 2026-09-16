using System.Security.Claims;

namespace ATMTicketing.Web.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static string? GetUserId(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier);

    public static string? GetFullName(this ClaimsPrincipal principal) =>
        principal.FindFirstValue("FullName");
}
