using Microsoft.Extensions.Options;
using VaporTrail.Options;

namespace VaporTrail.Services;

public sealed class SteamCatalogSyncHostedService(
    ISteamCatalogService catalogService,
    IOptions<CatalogOptions> options,
    ILogger<SteamCatalogSyncHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.AutoSyncOnStartup)
        {
            return;
        }

        // Let Kestrel finish binding before starting the background catalog sync.
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            await catalogService.SyncFromRemoteAsync(force: false, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Background Steam catalog sync failed.");
        }
    }
}
