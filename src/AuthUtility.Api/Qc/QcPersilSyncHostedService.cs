namespace AuthUtility.Api.Qc;

// Sync cache persil terjadwal. Sumber SQL Server hanya dibaca; kegagalan sync
// dicatat dan dicoba lagi pada interval berikutnya (tidak menghentikan API).
public class QcPersilSyncHostedService(
    IServiceScopeFactory scopes,
    IConfiguration config,
    ILogger<QcPersilSyncHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = config.GetValue("Qc:PersilSync:Enabled", true);
        if (!enabled)
        {
            logger.LogInformation("Sync persil QC terjadwal dinonaktifkan (Qc:PersilSync:Enabled=false).");
            return;
        }
        var minutes = Math.Clamp(config.GetValue("Qc:PersilSync:IntervalMinutes", 360), 5, 24 * 60);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(minutes));
        // Sync pertama shortly after startup supaya cache tidak kosong menunggu interval.
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var sync = scope.ServiceProvider.GetRequiredService<QcPersilSyncService>();
                var results = await sync.SyncAsync(null, "system-sync", stoppingToken);
                logger.LogInformation(
                    "Sync persil QC selesai: {Ringkasan}",
                    string.Join("; ", results.Select(r => $"{r.Sumber} dibaca={r.Dibaca} baru={r.Baru} ubah={r.Diperbarui} nonaktif={r.Dinonaktifkan}")));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception e)
            {
                logger.LogWarning(e, "Sync persil QC gagal; akan dicoba lagi pada interval berikutnya.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}