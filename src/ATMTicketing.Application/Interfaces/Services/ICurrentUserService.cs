namespace ATMTicketing.Application.Interfaces.Services;

/// <summary>Abstracts HttpContext.User away from the service layer so it stays framework-agnostic.</summary>
public interface ICurrentUserService
{
    string? UserId { get; }
    string? UserName { get; }
    string? FullName { get; }
    int? RegionId { get; }
    bool IsInRole(string role);
    string? IpAddress { get; }
}
