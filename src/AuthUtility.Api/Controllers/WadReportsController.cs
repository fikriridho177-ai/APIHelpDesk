using System.Security.Claims;
using AuthUtility.Api.Wad;
using AuthUtility.Application.DTOs.Security;
using AuthUtility.Application.Interfaces;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Api.Controllers;

// Laporan lapangan WAD (pengamatan air, subsiden gambut, lahan tergenang, progres projek).
// Setiap modul memeriksa kode menu WAD-nya sendiri agar hak per jabatan berlaku.
[ApiController]
[Route("api/wad")]
[Authorize]
public class WadReportsController(
    AppDbContext db,
    IPermissionService permissions,
    IConfiguration configuration,
    IWebHostEnvironment environment,
    ILogger<WadReportsController> logger
) : ControllerBase
{
    private const string DashboardMenu = "WAD_ECS_DASHBOARD";
    private const int MaxPageSize = 50;

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /* =========================================================
       MASTER OBJEK
    ========================================================= */

    [HttpGet("objects")]
    public async Task<IActionResult> GetObjects(string module, string? wilayah, CancellationToken ct)
    {
        var normalizedModule = NormalizeModule(module);
        if (normalizedModule == null) return BadRequest(new { message = "Modul laporan tidak dikenal." });
        if (!await HasPermission(MenuCodeForModule(normalizedModule), MenuPermission.Read)) return Forbid();

        var q = db.WadObjects.AsNoTracking().Where(x => x.Active && x.Module == normalizedModule);
        if (!string.IsNullOrWhiteSpace(wilayah)) q = q.Where(x => x.Wilayah == wilayah.Trim());

        var objects = await q.OrderBy(x => x.SortOrder).ThenBy(x => x.Code).ToListAsync(ct);
        var ids = objects.Select(x => x.Id).ToList();
        var latestReports = await db.WadReports.AsNoTracking()
            .Where(x => ids.Contains(x.ObjectId))
            .Select(x => new
            {
                x.Id,
                x.ObjectId,
                x.ObservedDate,
                x.ObservedTime,
                x.Condition,
                x.DropCm,
                x.ProgressPercent
            })
            .ToListAsync(ct);
        var latestByObject = latestReports
            .GroupBy(x => x.ObjectId)
            .ToDictionary(g => g.Key, g => g
                .OrderByDescending(x => x.ObservedDate)
                .ThenByDescending(x => x.ObservedTime.HasValue)
                .ThenByDescending(x => x.ObservedTime)
                .ThenByDescending(x => x.Id)
                .First());

        var latestIds = latestByObject.Values.Select(x => x.Id).ToList();
        var firstPhoto = (await db.WadReportPhotos.AsNoTracking()
                .Where(x => latestIds.Contains(x.ReportId))
                .OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
                .Select(x => new { x.ReportId, x.Id })
                .ToListAsync(ct))
            .GroupBy(x => x.ReportId)
            .ToDictionary(g => g.Key, g => (long?)g.First().Id);

        var items = objects.Select(x =>
        {
            latestByObject.TryGetValue(x.Id, out var latest);
            return new WadObjectDto(
                x.Id, x.Module, x.Code, x.Name, x.ObjectType, x.Wilayah, x.Location,
                x.ReferenceValueCm, x.AreaHa, x.PicName, x.StartDate, x.TargetDate,
                x.TargetQty, x.Unit,
                latest?.Id, latest?.ObservedDate, latest?.Condition, latest?.DropCm, latest?.ProgressPercent,
                latest is null ? null : firstPhoto.GetValueOrDefault(latest.Id),
                x.IsDemo, x.MeasurementMethod, x.VerifyDropCm, x.CriticalDropCm);
        }).ToList();

        return Ok(new { module = normalizedModule, total = items.Count, items });
    }

    /* =========================================================
       DAFTAR & DETAIL LAPORAN
    ========================================================= */

    [HttpGet("reports")]
    public async Task<IActionResult> GetReports(
        string module, string? wilayah, string? condition, int? objectId, string? objectType,
        DateOnly? from, DateOnly? to, int page = 1, int pageSize = 10,
        CancellationToken ct = default)
    {
        var normalizedModule = NormalizeModule(module);
        if (normalizedModule == null) return BadRequest(new { message = "Modul laporan tidak dikenal." });
        if (!await HasPermission(MenuCodeForModule(normalizedModule), MenuPermission.Read)) return Forbid();

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var q = db.WadReports.AsNoTracking().Where(x => x.Module == normalizedModule);
        if (!string.IsNullOrWhiteSpace(wilayah)) q = q.Where(x => x.Object!.Wilayah == wilayah.Trim());
        q = FilterCondition(q, normalizedModule, condition);
        if (objectId is > 0) q = q.Where(x => x.ObjectId == objectId.Value);
        if (!string.IsNullOrWhiteSpace(objectType)) q = q.Where(x => x.Object!.ObjectType == objectType);
        if (from is { } start) q = q.Where(x => x.ObservedDate >= start);
        if (to is { } end) q = q.Where(x => x.ObservedDate <= end);

        var total = await q.CountAsync(ct);
        var rows = await q
            .Include(x => x.Object)
            .Include(x => x.Photos)
            .AsSplitQuery()
            .OrderByDescending(x => x.ObservedDate)
            .ThenByDescending(x => x.ObservedTime.HasValue)
            .ThenByDescending(x => x.ObservedTime)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(new WadReportListDto(total, page, pageSize, rows.Select(ToDto).ToList()));
    }

    [HttpGet("reports/{id:long}")]
    public async Task<IActionResult> GetReport(long id, CancellationToken ct)
    {
        var report = await db.WadReports.AsNoTracking()
            .Include(x => x.Object)
            .Include(x => x.Photos)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (report == null) return NotFound(new { message = "Laporan tidak ditemukan." });
        var menuCode = MenuCodeForModule(report.Module);
        if (!await HasPermission(menuCode, MenuPermission.Read)) return Forbid();
        var canEdit = await HasPermission(menuCode, MenuPermission.Update)
            && (report.CreatedByUserId == CurrentUserId || await permissions.IsSuperAdminAsync(User));
        return Ok(ToDto(report) with { CanEdit = canEdit });
    }

    [HttpPost("reports")]
    public async Task<IActionResult> CreateReport([FromBody] WadReportInput input, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == CurrentUserId && x.Active, ct);
        if (user == null) return Forbid();

        var obj = await db.WadObjects.FirstOrDefaultAsync(x => x.Id == input.ObjectId && x.Active, ct);
        if (obj == null) return BadRequest(new { message = "Objek/lokasi tidak ditemukan pada data master." });
        if (!await HasPermission(MenuCodeForModule(obj.Module), MenuPermission.Create)) return Forbid();

        var error = Validate(obj.Module, input, obj);
        if (error != null) return BadRequest(new { message = error });

        if (input.RequestId == Guid.Empty) return BadRequest(new { message = "RequestId wajib diisi untuk mencegah laporan ganda." });
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(7611, {CurrentUserId})", ct);
        var existing = await db.WadReports.Include(x => x.Object).Include(x => x.Photos)
            .FirstOrDefaultAsync(x => x.CreatedByUserId == CurrentUserId && x.RequestId == input.RequestId, ct);
        if (existing != null) return Ok(ToDto(existing));

        var report = new WadReport
        {
            RequestId = input.RequestId,
            ObjectId = obj.Id,
            Module = obj.Module,
            CreatedByUserId = user.Id,
            CreatedByName = user.Name,
            CreatedAt = DateTime.UtcNow
        };
        Apply(report, obj, input);

        db.WadReports.Add(report);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        report.Object = obj;
        return Ok(ToDto(report));
    }

    [HttpPut("reports/{id:long}")]
    public async Task<IActionResult> UpdateReport(long id, [FromBody] WadReportInput input, CancellationToken ct)
    {
        var report = await db.WadReports
            .Include(x => x.Object)
            .Include(x => x.Photos)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (report == null) return NotFound(new { message = "Laporan tidak ditemukan." });
        if (!await HasPermission(MenuCodeForModule(report.Module), MenuPermission.Update)) return Forbid();
        if (report.CreatedByUserId != CurrentUserId && !await permissions.IsSuperAdminAsync(User)) return Forbid();

        var obj = await db.WadObjects.FirstOrDefaultAsync(x => x.Id == input.ObjectId && x.Active, ct);
        if (obj == null || obj.Module != report.Module)
            return BadRequest(new { message = "Objek/lokasi tidak sesuai dengan modul laporan." });

        var error = Validate(report.Module, input, obj);
        if (error != null) return BadRequest(new { message = error });

        Apply(report, obj, input);
        report.ObjectId = obj.Id;
        report.UpdatedAt = DateTime.UtcNow;
        report.UpdatedByUserId = CurrentUserId;
        await db.SaveChangesAsync(ct);

        report.Object = obj;
        return Ok(ToDto(report));
    }

    /* =========================================================
       RINGKASAN
    ========================================================= */

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        string? module, string? wilayah, DateOnly? from, DateOnly? to, int? objectId, string? condition, string? objectType,
        CancellationToken ct = default,
        [FromServices] MyKTTATestDbContext? iotDb = null)
    {
        if (string.IsNullOrWhiteSpace(module))
        {
            if (!await HasPermission(DashboardMenu, MenuPermission.Read)) return Forbid();
            if (iotDb == null)
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Sumber data logger WAD tidak tersedia." });
            return Ok(await WadDashboardService.BuildAsync(db, iotDb, ct));
        }

        var normalizedModule = NormalizeModule(module);
        if (normalizedModule == null) return BadRequest(new { message = "Modul laporan tidak dikenal." });
        if (!await HasPermission(MenuCodeForModule(normalizedModule), MenuPermission.Read)) return Forbid();

        var objectsQuery = db.WadObjects.AsNoTracking().Where(x => x.Active && x.Module == normalizedModule);
        if (!string.IsNullOrWhiteSpace(wilayah)) objectsQuery = objectsQuery.Where(x => x.Wilayah == wilayah.Trim());
        if (objectId is > 0) objectsQuery = objectsQuery.Where(x => x.Id == objectId);
        if (!string.IsNullOrWhiteSpace(objectType)) objectsQuery = objectsQuery.Where(x => x.ObjectType == objectType);
        var objects = await objectsQuery.ToListAsync(ct);
        var objectIds = objects.Select(x => x.Id).ToList();

        var reportsQuery = db.WadReports.AsNoTracking().Where(x => x.Module == normalizedModule && objectIds.Contains(x.ObjectId));
        if (from is { } start) reportsQuery = reportsQuery.Where(x => x.ObservedDate >= start);
        if (to is { } end) reportsQuery = reportsQuery.Where(x => x.ObservedDate <= end);

        var totalReports = await FilterCondition(reportsQuery, normalizedModule, condition).CountAsync(ct);
        var reports = await reportsQuery
            .Select(x => new
            {
                x.Id,
                x.ObjectId,
                x.ObservedDate,
                x.ObservedTime,
                x.Condition,
                x.DropCm,
                x.FloodAreaHa,
                x.FloodDurationDays,
                x.ProgressPercent
            })
            .ToListAsync(ct);

        var latest = reports
            .GroupBy(x => x.ObjectId)
            .Select(g => g
                .OrderByDescending(x => x.ObservedDate)
                .ThenByDescending(x => x.ObservedTime.HasValue)
                .ThenByDescending(x => x.ObservedTime)
                .ThenByDescending(x => x.Id)
                .First())
            .ToList();

        var todayWib = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        if (!string.IsNullOrWhiteSpace(condition))
        {
            var targets = objects.ToDictionary(x => x.Id, x => x.TargetDate);
            latest = latest.Where(x => (normalizedModule == WadReportModule.Project
                ? WadReportRules.ProjectStatus(x.ProgressPercent ?? 0, targets[x.ObjectId], todayWib)
                : x.Condition) == condition.Trim()).ToList();
            var matchingIds = latest.Select(x => x.ObjectId).ToHashSet();
            objects = objects.Where(x => matchingIds.Contains(x.Id)).ToList();
        }
        var latestDate = latest.Count == 0 ? (DateOnly?)null : latest.Max(x => x.ObservedDate);
        WadWaterSummary? water = null;
        WadPeatSummary? peat = null;
        WadFloodSummary? flood = null;
        WadProjectSummary? project = null;

        switch (normalizedModule)
        {
            case WadReportModule.Water:
                water = new WadWaterSummary(
                    objects.Count,
                    objects.Count(x => x.ObjectType == "PINTU_AIR"),
                    objects.Count(x => x.ObjectType == "SIFON"),
                    objects.Count(x => x.ObjectType == "GORONG_GORONG"),
                    latest.Count,
                    latest.Count(x => x.Condition == "BAIK"),
                    latest.Count(x => x.Condition == "PERLU_PERBAIKAN"),
                    latest.Count(x => x.Condition == "RUSAK"));
                break;

            case WadReportModule.Peat:
                var drops = latest.Where(x => x.DropCm != null).Select(x => x.DropCm!.Value).ToList();
                peat = new WadPeatSummary(
                    objects.Count,
                    totalReports,
                    latest.Count,
                    drops.Count == 0 ? null : Math.Round(drops.Average(), 2),
                    drops.Count == 0 ? null : drops.Max(),
                    latest.Count(x => x.Condition == "KRITIS"));
                break;

            case WadReportModule.Flood:
                var active = latest.Where(x => x.Condition != "SURUT").ToList();
                flood = new WadFloodSummary(
                    objects.Count,
                    active.Count,
                    active.Sum(x => x.FloodAreaHa ?? 0m),
                    active.Count(x => (x.FloodDurationDays ?? 0) > 7));
                break;

            case WadReportModule.Project:
                var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
                var progressByObject = latest.ToDictionary(x => x.ObjectId, x => x.ProgressPercent ?? 0);
                var states = objects.Select(x =>
                {
                    var percent = progressByObject.GetValueOrDefault(x.Id);
                    return new { Percent = percent, Status = WadReportRules.ProjectStatus(percent, x.TargetDate, today) };
                }).ToList();
                project = new WadProjectSummary(
                    objects.Count,
                    states.Count(x => x.Status == "SELESAI"),
                    states.Count(x => x.Status == "BERJALAN"),
                    states.Count(x => x.Status == "TERLAMBAT"),
                    states.Count == 0 ? 0 : (int)Math.Round(states.Average(x => x.Percent), MidpointRounding.AwayFromZero));
                break;
        }

        return Ok(new WadSummaryDto(
            normalizedModule, totalReports, latest.Count, latestDate, water, peat, flood, project));
    }

    /* =========================================================
       FOTO
    ========================================================= */

    [HttpPost("reports/{id:long}/photos")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> UploadPhoto(
        long id, IFormFile file, [FromForm] string? keterangan, [FromForm] string? phase, [FromForm] Guid requestId, CancellationToken ct)
    {
        if (file == null) return BadRequest(new { message = "Pilih berkas foto." });
        if (requestId == Guid.Empty) return BadRequest(new { message = "RequestId foto wajib diisi." });
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({id})", ct);

        var report = await db.WadReports
            .Include(x => x.Photos)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (report == null) return NotFound(new { message = "Laporan tidak ditemukan." });
        if (!await HasPermission(MenuCodeForModule(report.Module), MenuPermission.Create)
            && !await HasPermission(MenuCodeForModule(report.Module), MenuPermission.Update)) return Forbid();
        if (report.CreatedByUserId != CurrentUserId && !await permissions.IsSuperAdminAsync(User)) return Forbid();
        var existing = report.Photos.FirstOrDefault(x => x.RequestId == requestId);
        if (existing != null) return Ok(new WadPhotoDto(existing.Id, existing.Keterangan, existing.Phase, existing.SortOrder, PhotoUrl(existing.Id)));
        if (report.Photos.Count >= WadReportRules.MaxPhotos)
            return BadRequest(new { message = $"Maksimal {WadReportRules.MaxPhotos} foto per laporan." });

        if (keterangan?.Trim().Length > WadReportRules.MaxCaptionLength)
            return BadRequest(new { message = $"Keterangan foto maksimal {WadReportRules.MaxCaptionLength} karakter." });

        var normalizedPhase = NormalizePhase(report.Module, phase);
        if (normalizedPhase.error != null) return BadRequest(new { message = normalizedPhase.error });

        var signature = new byte[12];
        await using (var input = file.OpenReadStream())
        {
            var count = await input.ReadAtLeastAsync(signature, signature.Length, throwOnEndOfStream: false, cancellationToken: ct);
            var error = WadPhotoPolicy.Validate(file.FileName, file.ContentType, file.Length, signature.AsSpan(0, count));
            if (error != null) return BadRequest(new { message = error });
        }

        using (var imageStream = file.OpenReadStream())
        {
            var error = WadPhotoPolicy.ValidateImage(imageStream);
            if (error != null) return BadRequest(new { message = error });
        }

        var root = WadPhotoPolicy.Root(configuration, environment);
        var relativePath = WadPhotoPolicy.RelativePath(report.Module, report.Id, Path.GetExtension(file.FileName));
        var path = WadPhotoPolicy.ResolvePath(root, relativePath);
        if (path == null) return BadRequest(new { message = "Nama berkas foto tidak valid." });

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var commitStarted = false;
        try
        {
            await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await file.CopyToAsync(output, ct);
            if (new FileInfo(path).Length > WadReportRules.MaxPhotoBytes)
            {
                System.IO.File.Delete(path);
                return BadRequest(new { message = "Foto maksimal 5 MB." });
            }

            var photo = new WadReportPhoto
            {
                ReportId = report.Id,
                RequestId = requestId,
                FileUrl = relativePath,
                Keterangan = string.IsNullOrWhiteSpace(keterangan) ? null : keterangan.Trim(),
                Phase = normalizedPhase.value,
                SortOrder = report.Photos.Count,
                CreatedAt = DateTime.UtcNow
            };
            db.WadReportPhotos.Add(photo);
            report.UpdatedAt = DateTime.UtcNow;
            report.UpdatedByUserId = CurrentUserId;
            await db.SaveChangesAsync(ct);
            commitStarted = true;
            await transaction.CommitAsync(ct);
            return Ok(new WadPhotoDto(photo.Id, photo.Keterangan, photo.Phase, photo.SortOrder, PhotoUrl(photo.Id)));
        }
        catch
        {
            // Commit yang terputus bisa sudah berhasil; pembersih orphan akan memeriksa database.
            if (!commitStarted && System.IO.File.Exists(path)) System.IO.File.Delete(path);
            throw;
        }
    }

    [HttpDelete("reports/{id:long}/photos/{photoId:long}")]
    public async Task<IActionResult> DeletePhoto(long id, long photoId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({id})", ct);
        var report = await db.WadReports.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (report == null) return NotFound(new { message = "Laporan tidak ditemukan." });
        if (!await HasPermission(MenuCodeForModule(report.Module), MenuPermission.Update)) return Forbid();
        if (report.CreatedByUserId != CurrentUserId && !await permissions.IsSuperAdminAsync(User)) return Forbid();

        var photo = await db.WadReportPhotos.FirstOrDefaultAsync(x => x.Id == photoId && x.ReportId == id, ct);
        if (photo == null) return NotFound(new { message = "Foto tidak ditemukan." });

        db.WadReportPhotos.Remove(photo);
        report.UpdatedAt = DateTime.UtcNow;
        report.UpdatedByUserId = CurrentUserId;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var path = WadPhotoPolicy.ResolvePath(WadPhotoPolicy.Root(configuration, environment), photo.FileUrl);
        if (path != null)
        {
            try { System.IO.File.Delete(path); }
            catch (IOException error) { logger.LogWarning(error, "Berkas foto WAD {PhotoId} tidak dapat dibersihkan", photoId); }
            catch (UnauthorizedAccessException error) { logger.LogWarning(error, "Berkas foto WAD {PhotoId} tidak dapat dibersihkan", photoId); }
        }
        return NoContent();
    }

    // Foto tidak disajikan sebagai file publik; setiap baca diperiksa hak akses menu WAD.
    [HttpGet("photos/{photoId:long}")]
    public async Task<IActionResult> GetPhoto(long photoId, CancellationToken ct)
    {
        var photo = await db.WadReportPhotos.AsNoTracking()
            .Include(x => x.Report)
            .FirstOrDefaultAsync(x => x.Id == photoId, ct);
        if (photo == null) return NotFound();
        if (!await HasPermission(MenuCodeForModule(photo.Report?.Module), MenuPermission.Read)) return Forbid();

        var path = WadPhotoPolicy.ResolvePath(WadPhotoPolicy.Root(configuration, environment), photo.FileUrl);
        var contentType = path == null ? null : WadPhotoPolicy.ContentType(path);
        if (path == null || contentType == null || !System.IO.File.Exists(path)) return NotFound();

        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return PhysicalFile(path, contentType, enableRangeProcessing: true);
    }

    /* =========================================================
       HELPER
    ========================================================= */

    private Task<bool> HasPermission(string menuCode, MenuPermission permission) =>
        permissions.HasPermissionAsync(User, menuCode, permission);

    private static string MenuCodeForModule(string? module) => module switch
    {
        WadReportModule.Water => "WAD_ECS_PENGAMATAN_AIR",
        WadReportModule.Peat => "WAD_ECS_SUBSIDEN_GAMBUT",
        WadReportModule.Flood => "WAD_ECS_LAHAN_TERGENANG",
        WadReportModule.Project => "WAD_ECS_PROJEK",
        _ => "WAD_ECS_STASIUN"
    };

    private static string? NormalizeModule(string module)
    {
        var normalized = module?.Trim().ToUpperInvariant();
        return WadReportModule.IsValid(normalized) ? normalized : null;
    }

    private static string? Validate(string module, WadReportInput input, WadObject obj) => module switch
    {
        WadReportModule.Water => WadReportRules.ValidateWater(input),
        WadReportModule.Peat => WadReportRules.ValidatePeat(input, obj),
        WadReportModule.Flood => WadReportRules.ValidateFlood(input, obj),
        WadReportModule.Project => WadReportRules.ValidateProject(input, obj),
        _ => "Modul laporan tidak dikenal."
    };

    // Aturan simpan per modul; field modul lain direset agar hasil edit konsisten.
    private static void Apply(WadReport report, WadObject obj, WadReportInput input)
    {
        report.ObservedDate = input.ObservedDate;
        report.ObservedTime = input.ObservedTime;
        report.Condition = null;
        report.WaterLevelCm = null;
        report.MeasurementValue = null;
        report.MeasurementUnit = null;
        report.MeasuredValueCm = null;
        if (report.Id == 0 || report.ObjectId != obj.Id)
        {
            report.ReferenceValueCm = obj.ReferenceValueCm;
            report.MeasurementMethod = obj.MeasurementMethod;
        }
        report.DropCm = null;
        report.FloodAreaHa = null;
        report.FloodHeightCm = null;
        report.FloodStartDate = null;
        report.FloodDurationDays = null;
        report.WorkDescription = null;
        report.RealizationQty = null;
        report.RealizationUnit = null;
        report.ProgressPercent = null;
        report.Constraints = null;
        report.Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim();

        switch (report.Module)
        {
            case WadReportModule.Water:
                report.Condition = input.Condition;
                report.WaterLevelCm = input.WaterLevelCm;
                break;

            case WadReportModule.Peat:
                var unit = (input.MeasurementUnit ?? "CM").Trim().ToUpperInvariant();
                var measuredCm = unit == "M" ? input.MeasurementValue!.Value * 100m : input.MeasurementValue!.Value;
                report.MeasurementValue = input.MeasurementValue;
                report.MeasurementUnit = unit;
                report.MeasuredValueCm = measuredCm;
                report.DropCm = WadReportRules.PeatDrop(report.MeasurementMethod, report.ReferenceValueCm, measuredCm);
                report.Condition = WadReportRules.PeatCondition(report.DropCm, obj.VerifyDropCm, obj.CriticalDropCm);
                break;

            case WadReportModule.Flood:
                report.Condition = input.Condition;
                report.FloodAreaHa = input.FloodAreaHa;
                report.FloodHeightCm = input.FloodHeightCm;
                report.FloodStartDate = input.FloodStartDate;
                report.FloodDurationDays = WadReportRules.FloodDurationDays(input.ObservedDate, input.FloodStartDate);
                break;

            case WadReportModule.Project:
                report.WorkDescription = input.WorkDescription?.Trim();
                report.RealizationQty = input.RealizationQty;
                report.RealizationUnit = obj.Unit;
                report.ProgressPercent = WadReportRules.ProjectPercent(obj.TargetQty, input.RealizationQty, input.ProgressPercent);
                report.Constraints = string.IsNullOrWhiteSpace(input.Constraints) ? null : input.Constraints.Trim();
                break;
        }
    }

    private static (string? value, string? error) NormalizePhase(string module, string? phase)
    {
        if (module != WadReportModule.Project) return (null, null);
        var normalized = string.IsNullOrWhiteSpace(phase) ? WadPhotoPhase.During : phase.Trim().ToUpperInvariant();
        return WadPhotoPhase.All.Contains(normalized)
            ? (normalized, null)
            : (null, "Keterangan foto projek harus SEBELUM, PROSES, atau SESUDAH.");
    }

    private static WadReportDto ToDto(WadReport report)
    {
        var obj = report.Object ?? new WadObject();
        return new WadReportDto(
            report.Id,
            report.Module,
            report.ObjectId,
            obj.Code,
            obj.Name,
            obj.ObjectType,
            obj.Wilayah,
            obj.Location,
            report.ObservedDate,
            report.ObservedTime,
            report.Module == WadReportModule.Project
                ? WadReportRules.ProjectStatus(report.ProgressPercent ?? 0, obj.TargetDate, DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)))
                : report.Condition,
            report.WaterLevelCm,
            report.MeasurementValue,
            report.MeasurementUnit,
            report.MeasuredValueCm,
            report.ReferenceValueCm,
            report.DropCm,
            report.FloodAreaHa,
            report.FloodHeightCm,
            report.FloodStartDate,
            report.FloodDurationDays,
            report.WorkDescription,
            report.RealizationQty,
            report.RealizationUnit,
            report.ProgressPercent,
            report.Constraints,
            report.Notes,
            report.CreatedByUserId,
            report.CreatedByName,
            report.CreatedAt,
            report.UpdatedAt,
            report.Photos
                .OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
                .Select(x => new WadPhotoDto(x.Id, x.Keterangan, x.Phase, x.SortOrder, PhotoUrl(x.Id)))
                .ToList(), obj.IsDemo, report.MeasurementMethod);
    }

    private static IQueryable<WadReport> FilterCondition(IQueryable<WadReport> query, string module, string? condition)
    {
        if (string.IsNullOrWhiteSpace(condition)) return query;
        var value = condition.Trim();
        if (module != WadReportModule.Project) return query.Where(x => x.Condition == value);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        return value switch
        {
            "SELESAI" => query.Where(x => x.ProgressPercent >= 100),
            "TERLAMBAT" => query.Where(x => x.ProgressPercent < 100 && x.Object!.TargetDate < today),
            "BERJALAN" => query.Where(x => x.ProgressPercent < 100 && (x.Object!.TargetDate == null || x.Object.TargetDate >= today)),
            _ => query.Where(x => false)
        };
    }

    private static string PhotoUrl(long photoId) => $"/api/wad/photos/{photoId}";
}
