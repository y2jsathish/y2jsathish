namespace ATMTicketing.Application.Interfaces.Services;

public interface IAuditService
{
    Task LogAsync(string action, string entityName, string? entityId, object? oldValues, object? newValues, CancellationToken ct = default);
    Task LogLoginAsync(string userId, string userName, bool success, CancellationToken ct = default);
}
