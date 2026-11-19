using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AuthUtility.Api.IoT;

public sealed class IotDeviceStatusSweepOptions
{
    public int IntervalSeconds { get; set; } = 30;

    public int StaleAfterSeconds { get; set; } = 300;

    public int OfflineAfterSeconds { get; set; } = 1800;
}

/// <summary>
/// Menurunkan status perangkat berdasarkan umur heartbeat terakhir.
/// Tanpa ini status Online bertahan selamanya ketika perangkat mati dan
/// broker tidak mengirim pesan LWT offline.
/// </summary>
public sealed class IotDeviceStatusSweepService(
    IServiceScopeFactory scopeFactory,
    IOptions<IotDeviceStatusSweepOptions> options,
    ILogger<IotDeviceStatusSweepService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Clamp(options.Value.IntervalSeconds, 10, 3600));
        logger.LogInformation(
            "Sweep status perangkat IoT aktif setiap {Interval}s (stale {Stale}s, offline {Offline}s).",
            interval.TotalSeconds,
            options.Value.StaleAfterSeconds,
            options.Value.OfflineAfterSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Penyegaran status perangkat IoT gagal.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        var staleAfter = TimeSpan.FromSeconds(Math.Clamp(options.Value.StaleAfterSeconds, 60, 86400));
        var offlineAfter = TimeSpan.FromSeconds(Math.Clamp(options.Value.OfflineAfterSeconds, (int)staleAfter.TotalSeconds, 604800));

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTimeOffset.UtcNow;

        var devices = await db.IotDeviceAgents
            .Where(x => x.IsActive
                && (x.Status == IotDeviceStatus.Online || x.Status == IotDeviceStatus.Stale))
            .ToListAsync(cancellationToken);

        var changed = 0;
        foreach (var device in devices)
        {
            var next = NextStatus(device.Status, device.LastSeenAt, now, staleAfter, offlineAfter);
            if (next is null)
                continue;

            Transition(device, next.Value, now);
            changed++;
        }

        if (changed == 0)
            return;

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Status {Count} perangkat IoT disegarkan berdasarkan heartbeat terakhir.", changed);
    }

    /// <summary>
    /// Mengembalikan status berikutnya, atau null bila belum perlu berubah.
    /// Disabled tidak pernah disentuh; perangkat tanpa LastSeenAt dianggap Offline.
    /// </summary>
    public static IotDeviceStatus? NextStatus(
        IotDeviceStatus current,
        DateTimeOffset? lastSeenAt,
        DateTimeOffset now,
        TimeSpan staleAfter,
        TimeSpan offlineAfter)
    {
        if (current != IotDeviceStatus.Online && current != IotDeviceStatus.Stale)
            return null;

        if (lastSeenAt is null)
            return IotDeviceStatus.Offline;

        var age = now - lastSeenAt.Value;
        if (current == IotDeviceStatus.Online && age >= staleAfter)
            return IotDeviceStatus.Stale;

        if (current == IotDeviceStatus.Stale && age >= offlineAfter)
            return IotDeviceStatus.Offline;

        return null;
    }

    private static void Transition(IotDeviceAgent device, IotDeviceStatus status, DateTimeOffset now)
    {
        device.Status = status;
        device.UpdatedAt = now;
    }
}
