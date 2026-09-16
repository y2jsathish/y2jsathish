using ATMTicketing.Application.Interfaces.Services;

namespace ATMTicketing.Tests.TestSupport;

public class FakeCurrentUserService : ICurrentUserService
{
    public string? UserId { get; set; } = "test-user-id";
    public string? UserName { get; set; } = "test.user@atmticketing.local";
    public string? FullName { get; set; } = "Test User";
    public int? RegionId { get; set; }
    public string? IpAddress { get; set; } = "127.0.0.1";

    private readonly HashSet<string> _roles = new();

    public void AddRole(string role) => _roles.Add(role);

    public bool IsInRole(string role) => _roles.Contains(role);
}
