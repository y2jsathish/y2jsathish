using ATMTicketing.Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ATMTicketing.Infrastructure.BackgroundServices;

/// <summary>
/// Polls open tickets on a fixed interval, flags SLA breaches/warnings and fires notifications.
/// Runs as a hosted service so the SLA countdown stays accurate even with no user browsing the app.
/// </summary>
public class SlaMonitorService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SlaMonitorService> _logger;

    public SlaMonitorService(IServiceScopeFactory scopeFactory, ILogger<SlaMonitorService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var slaService = scope.ServiceProvider.GetRequiredService<ISlaService>();
                var changed = await slaService.EvaluateOpenTicketsAsync(stoppingToken);
                if (changed > 0)
                {
                    _logger.LogInformation("SLA monitor flagged {Count} ticket(s) as breached.", changed);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "SLA monitor sweep failed.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // shutting down
            }
        }
    }
}
