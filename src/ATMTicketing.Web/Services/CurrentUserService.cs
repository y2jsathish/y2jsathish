using ATMTicketing.Application.Interfaces.Services;
using ATMTicketing.Web.Extensions;

namespace ATMTicketing.Web.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private System.Security.Claims.ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public string? UserId => User?.GetUserId();
    public string? UserName => User?.Identity?.Name;
    public string? FullName => User?.GetFullName();
    public int? RegionId
    {
        get
        {
            var claim = User?.FindFirst("RegionId")?.Value;
            return int.TryParse(claim, out var regionId) ? regionId : null;
        }
    }

    public bool IsInRole(string role) => User?.IsInRole(role) ?? false;

    public string? IpAddress => _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}
