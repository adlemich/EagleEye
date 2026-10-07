namespace EagleEye.Service.UserAccounts;

/// <summary>
/// Checks the local accounts every <see cref="CheckInterval"/> (US-003 plan, "how the service detects
/// account changes"): <see cref="IUserAccountService.RefreshInventoryAsync"/> broadcasts only if
/// something changed. A failed check is logged and the loop goes on (coding guidelines §5.3).
/// </summary>
public sealed class AccountInventoryMonitor(
    IUserAccountService userAccounts,
    TimeProvider timeProvider,
    ILogger<AccountInventoryMonitor> logger) : BackgroundService
{
    /// <summary>Interval of the check (Michael, 2026-10-07, Q-3). A constant until the service has its YAML configuration.</summary>
    public static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(15);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CheckInterval, timeProvider);
        try
        {
            while (true)
            {
                // The result is always true: the local timer is only disposed after the loop has ended.
                _ = await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false);
                await CheckAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The service stops.
        }
    }

    private async Task CheckAsync(CancellationToken stoppingToken)
    {
        try
        {
            await userAccounts.RefreshInventoryAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            // Background loop boundary: one failed check must not end the monitor.
            logger.LogError(ex, "Checking the account inventory failed.");
        }
    }
}
