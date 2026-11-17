using AuthUtility.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Api.Wad;

public sealed class WadPhotoCleanupService(IServiceScopeFactory scopes, IConfiguration configuration,
    IWebHostEnvironment environment, ILogger<WadPhotoCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        do
        {
            try
            {
                var root = WadPhotoPolicy.Root(configuration, environment);
                if (!Directory.Exists(root)) continue;
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var cutoff = DateTime.UtcNow.AddHours(-24);
                foreach (var file in Directory.EnumerateFiles(root, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint }))
                {
                    stoppingToken.ThrowIfCancellationRequested();
                    if (WadPhotoPolicy.ContentType(file) == null || File.GetLastWriteTimeUtc(file) >= cutoff) continue;
                    if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(file), "N", out _)) continue;
                    var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                    if (!await db.WadReportPhotos.AnyAsync(x => x.FileUrl == relative, stoppingToken)) File.Delete(file);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception error) { logger.LogWarning(error, "Pembersihan foto WAD gagal; akan dicoba pada jadwal berikutnya"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
