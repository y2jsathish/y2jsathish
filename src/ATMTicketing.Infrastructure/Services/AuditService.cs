using System.Text.Json;
using ATMTicketing.Application.Interfaces.Repositories;
using ATMTicketing.Application.Interfaces.Services;
using ATMTicketing.Domain.Entities;

namespace ATMTicketing.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public AuditService(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task LogAsync(string action, string entityName, string? entityId, object? oldValues, object? newValues, CancellationToken ct = default)
    {
        await _uow.AuditLogs.AddAsync(new AuditLog
        {
            UserId = _currentUser.UserId,
            UserName = _currentUser.UserName,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues),
            IpAddress = _currentUser.IpAddress,
            Timestamp = DateTime.UtcNow
        }, ct);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task LogLoginAsync(string userId, string userName, bool success, CancellationToken ct = default)
    {
        await _uow.AuditLogs.AddAsync(new AuditLog
        {
            UserId = userId,
            UserName = userName,
            Action = success ? "LOGIN_SUCCESS" : "LOGIN_FAILED",
            EntityName = "User",
            EntityId = userId,
            IpAddress = _currentUser.IpAddress,
            Timestamp = DateTime.UtcNow
        }, ct);
        await _uow.SaveChangesAsync(ct);
    }
}
