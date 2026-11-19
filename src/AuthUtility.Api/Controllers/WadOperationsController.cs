using System.Security.Claims;
using System.Text.Json;
using AuthUtility.Api.Wad;
using AuthUtility.Application.Interfaces;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Api.Controllers;

// Data operasional WAD: hasil impor "1. Helpdesk ASD" + join view sensor eksisting.
[ApiController]
[Route("api/wad")]
[Authorize]
public class WadOperationsController(
    AppDbContext db,
    MyKTTATestDbContext iotDb,
    IPermissionService permissions,
    IConfiguration configuration,
    IWebHostEnvironment environment,
    ILogger<WadOperationsController> logger
) : ControllerBase
{
    private const string PengamatanMenu = "WAD_ECS_PENGAMATAN_AIR";
    private const string SubsidenMenu = "WAD_ECS_SUBSIDEN_GAMBUT";
    private const string LahanMenu = "WAD_ECS_LAHAN_TERGENANG";
    private static readonly DateOnly SeriesStart = new(2025, 12, 29);

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /* =========================================================
       PENGAMATAN AIR
    ========================================================= */

    [HttpGet("pengamatan-air")]
    public async Task<IActionResult> GetPengamatanAir(
        [FromQuery] string? wilayah,
        [FromQuery] string? jenis,
        [FromQuery] string? statusOperasi,
        [FromQuery] string? search,
        [FromQuery] bool? perluPerhatian,
        [FromQuery] DateOnly? dari,
        [FromQuery] DateOnly? sampai,
        CancellationToken ct)
    {
        if (!await HasPermission(PengamatanMenu, MenuPermission.Read)) return Forbid();

        var buildingsQuery = db.WadBuildings.AsNoTracking().Where(x => x.Aktif);
        if (!string.IsNullOrWhiteSpace(wilayah)) buildingsQuery = buildingsQuery.Where(x => x.Wilayah == wilayah.Trim());
        if (!string.IsNullOrWhiteSpace(jenis))
        {
            var normalizedJenis = jenis.Trim().ToUpperInvariant();
            buildingsQuery = buildingsQuery.Where(x => x.JenisBangunan.ToUpper() == normalizedJenis);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            buildingsQuery = buildingsQuery.Where(x =>
                x.NamaBangunan.ToLower().Contains(term)
                || x.Kanal.ToLower().Contains(term)
                || x.KodeAset.ToLower().Contains(term));
        }

        var buildings = await buildingsQuery
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.KodeAset)
            .ToListAsync(ct);

        var latestLogSql = (dari.HasValue, sampai.HasValue) switch
        {
            (true, true) => """
                SELECT DISTINCT ON (kode_aset) *
                FROM trx_wad_log_overflow
                WHERE tanggal_log >= {0} AND tanggal_log <= {1}
                ORDER BY kode_aset, tanggal_log DESC, id DESC
                """,
            (true, false) => """
                SELECT DISTINCT ON (kode_aset) *
                FROM trx_wad_log_overflow
                WHERE tanggal_log >= {0}
                ORDER BY kode_aset, tanggal_log DESC, id DESC
                """,
            (false, true) => """
                SELECT DISTINCT ON (kode_aset) *
                FROM trx_wad_log_overflow
                WHERE tanggal_log <= {0}
                ORDER BY kode_aset, tanggal_log DESC, id DESC
                """,
            _ => """
                SELECT DISTINCT ON (kode_aset) *
                FROM trx_wad_log_overflow
                ORDER BY kode_aset, tanggal_log DESC, id DESC
                """,
        };
        var latestLogDates = new List<object>();
        if (dari.HasValue) latestLogDates.Add(dari.Value);
        if (sampai.HasValue) latestLogDates.Add(sampai.Value);
        var latestLogs = await db.WadOverflowLogs
            .FromSqlRaw(latestLogSql, latestLogDates.ToArray())
            .AsNoTracking()
            .ToListAsync(ct);
        var latestByKode = latestLogs.ToDictionary(x => x.KodeAset, StringComparer.OrdinalIgnoreCase);

        var logIds = latestLogs.Select(x => x.Id).ToList();
        var photos = await db.WadOverflowLogPhotos.AsNoTracking()
            .Where(x => logIds.Contains(x.LogId))
            .OrderBy(x => x.Id)
            .Select(x => new { x.LogId, x.Id })
            .ToListAsync(ct);
        var firstPhoto = photos
            .GroupBy(x => x.LogId)
            .ToDictionary(group => group.Key, group => (long?)group.First().Id);

        var warnings = new List<string>();
        var todayWib = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

        var tmakValues = new Dictionary<string, WadHydrologyReader.TmakPoint>(StringComparer.OrdinalIgnoreCase);
        var tmatValues = new Dictionary<string, WadHydrologyReader.TmatPoint>(StringComparer.OrdinalIgnoreCase);
        var rainPoints = new List<WadHydrologyReader.RainPoint>();
        try { tmakValues = await WadHydrologyReader.ReadLatestTmak(iotDb, ct); }
        catch (Exception error) { logger.LogWarning(error, "Nilai TMAK tidak dapat dibaca"); warnings.Add("Nilai TMAK belum dapat dimuat dari sumber sensor."); }
        try { tmatValues = await WadHydrologyReader.ReadLatestTmat(iotDb, ct); }
        catch (Exception error) { logger.LogWarning(error, "Nilai TMAT tidak dapat dibaca"); warnings.Add("Nilai TMAT belum dapat dimuat dari sumber sensor."); }
        try
        {
            rainPoints = await WadHydrologyReader.ReadLatestRain(
                iotDb, todayWib.AddDays(-29).ToDateTime(TimeOnly.MinValue), ct);
        }
        catch (Exception error) { logger.LogWarning(error, "Nilai curah hujan tidak dapat dibaca"); warnings.Add("Nilai curah hujan belum dapat dimuat dari sumber sensor."); }

        var rainByWilayah = new Dictionary<string, WadAirRainValue>(StringComparer.OrdinalIgnoreCase);
        foreach (var region in buildings.Select(x => x.Wilayah).Distinct())
            rainByWilayah[region] = WadHydrologyReader.BuildRegionRain(
                region, rainPoints, todayWib.ToDateTime(TimeOnly.MinValue));

        var items = buildings.Select(building =>
        {
            latestByKode.TryGetValue(building.KodeAset, out var log);
            firstPhoto.TryGetValue(log?.Id ?? 0, out var photoId);
            return new WadAirItemDto(
                building.KodeAset,
                building.Wilayah,
                building.Kanal,
                building.NamaBangunan,
                building.UkuranDimensi,
                building.JumlahPapan,
                building.JenisBangunan,
                log?.StatusOperasi,
                log?.DetailBukaan,
                log?.KondisiFisik,
                WadDashboardService.NeedsAttention(log?.KondisiFisik),
                WadDashboardService.AlertLevel(log?.KondisiFisik),
                log?.TanggalLog.ToString("yyyy-MM-dd"),
                log?.Minggu,
                WadHydrologyReader.ToValue(building.RefTmakLokasi, tmakValues, "cm"),
                WadHydrologyReader.ToValue(building.RefTpmatLokasi, tmatValues, "cm"),
                rainByWilayah.GetValueOrDefault(building.Wilayah) ?? new WadAirRainValue(null, null, null, 0),
                log?.PetugasNama,
                log?.CreatedAt,
                photoId != null,
                photoId);
        }).ToList();

        if (!string.IsNullOrWhiteSpace(statusOperasi))
        {
            var normalized = NormalizeStatus(statusOperasi);
            items = items.Where(x => string.Equals(x.StatusOperasi, normalized, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        if (perluPerhatian == true)
            items = items.Where(x => x.PerluPerhatian).ToList();

        var latestDate = latestLogs.Count == 0 ? null : latestLogs.Max(x => x.TanggalLog).ToString("yyyy-MM-dd");
        return Ok(new WadAirListDto(items.Count, latestDate, items, warnings));
    }

    [HttpGet("pengamatan-air/{kodeAset}/riwayat")]
    public async Task<IActionResult> GetPengamatanAirHistory(string kodeAset, CancellationToken ct)
    {
        if (!await HasPermission(PengamatanMenu, MenuPermission.Read)) return Forbid();

        var building = await db.WadBuildings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.KodeAset == kodeAset, ct);
        if (building == null) return NotFound(new { message = "Bangunan air tidak ditemukan." });

        var logs = await db.WadOverflowLogs.AsNoTracking()
            .Where(x => x.KodeAset == building.KodeAset)
            .OrderBy(x => x.TanggalLog)
            .ThenBy(x => x.Id)
            .ToListAsync(ct);
        var logIds = logs.Select(x => x.Id).ToList();
        var photos = await db.WadOverflowLogPhotos.AsNoTracking()
            .Where(x => logIds.Contains(x.LogId))
            .OrderBy(x => x.Id)
            .Select(x => new { x.LogId, x.Id })
            .ToListAsync(ct);
        var firstPhoto = photos
            .GroupBy(x => x.LogId)
            .ToDictionary(group => group.Key, group => (long?)group.First().Id);

        var items = logs.Select(log => new WadOverflowLogDto(
            log.Id,
            log.Minggu,
            log.TanggalLog.ToString("yyyy-MM-dd"),
            log.StatusOperasi,
            log.DetailBukaan,
            log.KondisiFisik,
            log.PetugasNama,
            log.PemeriksaNama,
            firstPhoto.GetValueOrDefault(log.Id),
            log.CreatedAt)).ToList();

        return Ok(new WadAirHistoryDto(
            building.KodeAset,
            building.Wilayah,
            building.Kanal,
            building.NamaBangunan,
            building.UkuranDimensi,
            building.JumlahPapan,
            items));
    }

    [HttpPost("pengamatan-air/log")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> CreatePengamatanAirLog(
        [FromForm] string kodeAset,
        [FromForm] DateOnly tanggal,
        [FromForm] string statusOperasi,
        [FromForm] string? detailBukaan,
        [FromForm] string? kondisiFisik,
        [FromForm] string? petugasNama,
        [FromForm] string? pemeriksaNama,
        [FromForm] Guid requestId,
        IFormFile? foto,
        CancellationToken ct)
    {
        if (!await HasPermission(PengamatanMenu, MenuPermission.Create)) return Forbid();
        if (requestId == Guid.Empty) return BadRequest(new { message = "RequestId wajib diisi untuk mencegah laporan ganda." });

        var building = await db.WadBuildings.FirstOrDefaultAsync(x => x.KodeAset == kodeAset.Trim() && x.Aktif, ct);
        if (building == null) return BadRequest(new { message = "Bangunan air tidak ditemukan atau tidak aktif." });

        var normalizedStatus = NormalizeStatus(statusOperasi);
        if (normalizedStatus == null) return BadRequest(new { message = "Status operasi harus Buka atau Tutup." });
        if (detailBukaan?.Trim().Length > 250) return BadRequest(new { message = "Detail bukaan maksimal 250 karakter." });
        if (kondisiFisik?.Trim().Length > 250) return BadRequest(new { message = "Kondisi fisik maksimal 250 karakter." });
        if (tanggal == default) return BadRequest(new { message = "Tanggal log wajib diisi." });

        var existing = await db.WadOverflowLogs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.RequestId == requestId, ct);
        if (existing != null)
            return Ok(ToLogDto(existing));

        var duplicate = await db.WadOverflowLogs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.KodeAset == building.KodeAset && x.TanggalLog == tanggal, ct);
        if (duplicate != null)
            return Conflict(new { message = $"Log tanggal {tanggal:yyyy-MM-dd} untuk {building.NamaBangunan} sudah ada." });

        if (foto != null)
        {
            var signature = new byte[12];
            await using var input = foto.OpenReadStream();
            var count = await input.ReadAtLeastAsync(signature, signature.Length, throwOnEndOfStream: false, cancellationToken: ct);
            var error = WadPhotoPolicy.Validate(foto.FileName, foto.ContentType, foto.Length, signature.AsSpan(0, count));
            if (error != null) return BadRequest(new { message = error });
            using var imageStream = foto.OpenReadStream();
            var imageError = WadPhotoPolicy.ValidateImage(imageStream);
            if (imageError != null) return BadRequest(new { message = imageError });
        }

        var user = await db.Users.AsNoTracking()
            .Where(x => x.Id == CurrentUserId)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(ct);

        var log = new WadOverflowLog
        {
            KodeAset = building.KodeAset,
            Minggu = WeekCode(tanggal),
            TanggalLog = tanggal,
            StatusOperasi = normalizedStatus,
            DetailBukaan = string.IsNullOrWhiteSpace(detailBukaan) ? null : detailBukaan.Trim(),
            KondisiFisik = string.IsNullOrWhiteSpace(kondisiFisik) ? null : kondisiFisik.Trim(),
            PetugasNama = string.IsNullOrWhiteSpace(petugasNama) ? user : petugasNama.Trim(),
            PemeriksaNama = string.IsNullOrWhiteSpace(pemeriksaNama) ? null : pemeriksaNama.Trim(),
            RequestId = requestId,
            CreatedAt = DateTime.UtcNow,
        };

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        string? filePath = null;
        var committed = false;
        try
        {
            db.WadOverflowLogs.Add(log);
            await db.SaveChangesAsync(ct);

            if (foto != null)
            {
                var root = WadPhotoPolicy.Root(configuration, environment);
                var relativePath = WadPhotoPolicy.RelativePath("overflow", log.Id, Path.GetExtension(foto.FileName));
                filePath = WadPhotoPolicy.ResolvePath(root, relativePath);
                if (filePath == null) return BadRequest(new { message = "Nama berkas foto tidak valid." });

                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                await using (var output = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    await foto.CopyToAsync(output, ct);

                db.WadOverflowLogPhotos.Add(new WadOverflowLogPhoto
                {
                    LogId = log.Id,
                    FileUrl = relativePath,
                    CreatedAt = DateTime.UtcNow,
                });
                await db.SaveChangesAsync(ct);
            }

            building.KondisiTerakhir = log.KondisiFisik;
            building.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            committed = true;
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException error) when (IsUniqueViolation(error))
        {
            if (!committed && filePath != null && System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
            return Conflict(new { message = "Log atau foto dengan referensi yang sama sudah tersimpan." });
        }
        catch
        {
            if (!committed && filePath != null && System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
            throw;
        }

        var photo = await db.WadOverflowLogPhotos.AsNoTracking()
            .Where(x => x.LogId == log.Id)
            .OrderBy(x => x.Id)
            .Select(x => (long?)x.Id)
            .FirstOrDefaultAsync(ct);
        return Ok(ToLogDto(log) with { FotoId = photo });
    }

    [HttpGet("pengamatan-air/foto/{photoId:long}")]
    public async Task<IActionResult> GetPengamatanAirPhoto(long photoId, CancellationToken ct)
    {
        if (!await HasPermission(PengamatanMenu, MenuPermission.Read)) return Forbid();

        var photo = await db.WadOverflowLogPhotos.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == photoId, ct);
        if (photo == null) return NotFound();

        var path = WadPhotoPolicy.ResolvePath(WadPhotoPolicy.Root(configuration, environment), photo.FileUrl);
        var contentType = path == null ? null : WadPhotoPolicy.ContentType(path);
        if (path == null || contentType == null || !System.IO.File.Exists(path)) return NotFound();

        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return PhysicalFile(path, contentType, enableRangeProcessing: true);
    }

    /* =========================================================
       SUBSIDEN GAMBUT
    ========================================================= */

    [HttpGet("subsiden")]
    public async Task<IActionResult> GetSubsiden(CancellationToken ct)
    {
        if (!await HasPermission(SubsidenMenu, MenuPermission.Read)) return Forbid();

        var observations = await db.WadSubsidenObservations.AsNoTracking()
            .OrderBy(x => x.Periode)
            .ThenBy(x => x.KodePatok)
            .ToListAsync(ct);

        var photos = await db.WadSubsidenPhotos.AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .Select(x => new { x.KodePatok, x.Periode, x.Id })
            .ToListAsync(ct);
        var photoBySlot = photos
            .GroupBy(x => (x.KodePatok, x.Periode))
            .ToDictionary(group => group.Key, group => (long?)group.First().Id);

        var periods = observations
            .GroupBy(x => new { x.Periode, x.PeriodeLabel })
            .OrderBy(group => group.Key.Periode)
            .Select(group => new WadSubsidenPeriodDto(group.Key.Periode, group.Key.PeriodeLabel))
            .ToList();

        var stakes = observations
            .GroupBy(x => x.KodePatok)
            .Select(group =>
            {
                var ordered = group.OrderBy(x => x.Periode).ToList();
                var first = ordered[0];
                var values = new List<WadSubsidenValueDto>(ordered.Count);
                decimal? previous = null;
                foreach (var observation in ordered)
                {
                    var delta = previous == null ? (decimal?)null : observation.NilaiCm - previous.Value;
                    photoBySlot.TryGetValue((observation.KodePatok, observation.Periode), out var fotoId);
                    values.Add(new WadSubsidenValueDto(observation.Periode, observation.NilaiCm, delta, fotoId));
                    previous = observation.NilaiCm;
                }

                return new WadSubsidenStakeDto(
                    group.Key,
                    first.Wilayah,
                    first.Blok,
                    first.LintangRaw,
                    first.Lintang,
                    first.BujurRaw,
                    first.Bujur,
                    first.TahunTanam,
                    first.KedalamanGambut,
                    ordered[^1].NilaiCm,
                    values);
            })
            .OrderBy(x => x.KodePatok)
            .ToList();

        var averages = periods.Select(period =>
        {
            var periodValues = observations
                .Where(x => x.Periode == period.Key)
                .Select(x => (decimal?)x.NilaiCm)
                .ToList();
            return new WadSubsidenAverageDto(
                period.Key,
                period.Label,
                periodValues.Count == 0
                    ? null
                    : Math.Round(periodValues.Average() ?? 0, 2, MidpointRounding.AwayFromZero));
        }).ToList();

        return Ok(new WadSubsidenResponse(periods, stakes, averages, "September_Subsiden 2026.xlsx"));
    }

    [HttpGet("subsiden/foto/{photoId:long}")]
    public async Task<IActionResult> GetSubsidenPhoto(long photoId, CancellationToken ct)
    {
        if (!await HasPermission(SubsidenMenu, MenuPermission.Read)) return Forbid();

        var photo = await db.WadSubsidenPhotos.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == photoId, ct);
        if (photo == null) return NotFound();

        var path = WadPhotoPolicy.ResolvePath(WadPhotoPolicy.Root(configuration, environment), photo.FileUrl);
        var contentType = path == null ? null : WadPhotoPolicy.ContentType(path);
        if (path == null || contentType == null || !System.IO.File.Exists(path)) return NotFound();

        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return PhysicalFile(path, contentType, enableRangeProcessing: true);
    }

    /* =========================================================
       BLANGKO SIPON & LAHAN TERGENANG
    ========================================================= */

    [HttpGet("sipon")]
    public async Task<IActionResult> GetSipon(CancellationToken ct)
    {
        if (!await HasPermission(PengamatanMenu, MenuPermission.Read)) return Forbid();

        var records = await db.WadSiponInspections.AsNoTracking()
            .OrderByDescending(x => x.Tanggal)
            .ThenByDescending(x => x.Id)
            .ToListAsync(ct);
        var items = records.Select(ToSiponDto).ToList();

        var template = new WadSiponTemplateDto(
            new[] { "Persil", "Lintang", "Bujur", "Tanggal", "Kondisi", "TMA Inlet (cm)", "TMA Outlet (cm)", "Tanggul Inlet", "Tanggul Outlet", "Komponen", "Keterangan" },
            new[] { "B", "KB", "RB" },
            new[]
            {
                new WadSiponChecklistItem(1, "Pintu air / bangunan sipon"),
                new WadSiponChecklistItem(2, "Dinding dan lantai"),
                new WadSiponChecklistItem(3, "Tanggul inlet"),
                new WadSiponChecklistItem(4, "Tanggul outlet"),
            });
        return Ok(new WadSiponResponse(template, items));
    }

    [HttpPost("sipon")]
    public async Task<IActionResult> CreateSipon([FromBody] WadSiponInput input, CancellationToken ct)
    {
        if (!await HasPermission(PengamatanMenu, MenuPermission.Create)) return Forbid();

        if (string.IsNullOrWhiteSpace(input.Persil)) return BadRequest(new { message = "Persil wajib diisi." });
        if (input.Tanggal == default) return BadRequest(new { message = "Tanggal wajib diisi." });
        var kondisi = NormalizeChoice(input.Kondisi, "B", "KB", "RB");
        if (kondisi == null) return BadRequest(new { message = "Kondisi harus B, KB, atau RB." });

        var record = new WadSiponInspection
        {
            Persil = input.Persil.Trim(),
            Lintang = input.Lintang,
            Bujur = input.Bujur,
            Tanggal = input.Tanggal,
            Kondisi = kondisi,
            TmaInletCm = input.TmaInletCm,
            TmaOutletCm = input.TmaOutletCm,
            TanggulInlet = NormalizeChoice(input.TanggulInlet, "B", "KB", "RB"),
            TanggulOutlet = NormalizeChoice(input.TanggulOutlet, "B", "KB", "RB"),
            Komponen = JsonSerializer.Serialize(input.Komponen ?? Array.Empty<string>()),
            Keterangan = string.IsNullOrWhiteSpace(input.Keterangan) ? null : input.Keterangan.Trim(),
            CreatedBy = await CurrentUserName(ct),
            CreatedAt = DateTime.UtcNow,
        };
        db.WadSiponInspections.Add(record);
        await db.SaveChangesAsync(ct);
        return Ok(ToSiponDto(record));
    }

    [HttpGet("lahan-tergenang")]
    public async Task<IActionResult> GetLahanTergenang(CancellationToken ct)
    {
        if (!await HasPermission(LahanMenu, MenuPermission.Read)) return Forbid();

        var records = await db.WadFloodedLandObservations.AsNoTracking()
            .OrderByDescending(x => x.Tanggal)
            .ThenByDescending(x => x.Id)
            .ToListAsync(ct);
        var items = records.Select(ToLahanDto).ToList();

        var template = new WadLahanTemplateDto(
            new[] { "Persil", "Lintang", "Bujur", "Tanggal", "Kondisi", "TMAT (cm)", "Luas (Ha)", "Akses Jalan", "Tanggul", "Keterangan" },
            new[] { "LP", "T" },
            new[] { "B", "S" },
            new[] { "B", "J" });
        return Ok(new WadLahanResponse(template, items));
    }

    [HttpPost("lahan-tergenang")]
    public async Task<IActionResult> CreateLahanTergenang([FromBody] WadLahanInput input, CancellationToken ct)
    {
        if (!await HasPermission(LahanMenu, MenuPermission.Create)) return Forbid();

        if (string.IsNullOrWhiteSpace(input.Persil)) return BadRequest(new { message = "Persil wajib diisi." });
        if (input.Tanggal == default) return BadRequest(new { message = "Tanggal wajib diisi." });
        var kondisi = NormalizeChoice(input.Kondisi, "LP", "T");
        if (kondisi == null) return BadRequest(new { message = "Kondisi harus LP atau T." });

        var record = new WadFloodedLandObservation
        {
            Persil = input.Persil.Trim(),
            Lintang = input.Lintang,
            Bujur = input.Bujur,
            Tanggal = input.Tanggal,
            Kondisi = kondisi,
            TmatCm = input.TmatCm,
            LuasHa = input.LuasHa,
            AksesJalan = NormalizeChoice(input.AksesJalan, "B", "S"),
            Tanggul = NormalizeChoice(input.Tanggul, "B", "J"),
            Keterangan = string.IsNullOrWhiteSpace(input.Keterangan) ? null : input.Keterangan.Trim(),
            CreatedBy = await CurrentUserName(ct),
            CreatedAt = DateTime.UtcNow,
        };
        db.WadFloodedLandObservations.Add(record);
        await db.SaveChangesAsync(ct);
        return Ok(ToLahanDto(record));
    }

    /* =========================================================
       HELPER
    ========================================================= */

    private Task<bool> HasPermission(string menuCode, MenuPermission permission) =>
        permissions.HasPermissionAsync(User, menuCode, permission);

    private async Task<string> CurrentUserName(CancellationToken ct) =>
        await db.Users.AsNoTracking()
            .Where(x => x.Id == CurrentUserId)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(ct)
        ?? User.Identity?.Name
        ?? "Sistem";

    private static string? NormalizeStatus(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "buka" => "Buka",
            "tutup" => "Tutup",
            _ => null,
        };
    }

    private static string? NormalizeChoice(string? value, params string[] allowed)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim().ToUpperInvariant();
        return allowed.Contains(normalized, StringComparer.Ordinal) ? normalized : null;
    }

    // M01 dimulai 29 Desember 2025 mengikuti seri mingguan sumber.
    private static string WeekCode(DateOnly date)
    {
        var week = ((date.DayNumber - SeriesStart.DayNumber) / 7) + 1;
        return $"M{Math.Max(week, 1):00}";
    }

    private static bool IsUniqueViolation(DbUpdateException error) =>
        error.InnerException is Npgsql.PostgresException { SqlState: "23505" };

    private static WadOverflowLogDto ToLogDto(WadOverflowLog log) => new(
        log.Id,
        log.Minggu,
        log.TanggalLog.ToString("yyyy-MM-dd"),
        log.StatusOperasi,
        log.DetailBukaan,
        log.KondisiFisik,
        log.PetugasNama,
        log.PemeriksaNama,
        null,
        log.CreatedAt);

    private static WadSiponRecordDto ToSiponDto(WadSiponInspection record) => new(
        record.Id,
        record.Persil,
        record.Tanggal.ToString("yyyy-MM-dd"),
        record.Kondisi,
        record.TmaInletCm,
        record.TmaOutletCm,
        record.TanggulInlet,
        record.TanggulOutlet,
        record.Komponen,
        record.Keterangan,
        record.CreatedBy,
        record.CreatedAt);

    private static WadLahanRecordDto ToLahanDto(WadFloodedLandObservation record) => new(
        record.Id,
        record.Persil,
        record.Tanggal.ToString("yyyy-MM-dd"),
        record.Kondisi,
        record.TmatCm,
        record.LuasHa,
        record.AksesJalan,
        record.Tanggul,
        record.Keterangan,
        record.CreatedBy,
        record.CreatedAt);
}

public sealed record WadSiponInput(
    string? Persil,
    decimal? Lintang,
    decimal? Bujur,
    DateOnly Tanggal,
    string? Kondisi,
    decimal? TmaInletCm,
    decimal? TmaOutletCm,
    string? TanggulInlet,
    string? TanggulOutlet,
    string[]? Komponen,
    string? Keterangan);

public sealed record WadLahanInput(
    string? Persil,
    decimal? Lintang,
    decimal? Bujur,
    DateOnly Tanggal,
    string? Kondisi,
    decimal? TmatCm,
    decimal? LuasHa,
    string? AksesJalan,
    string? Tanggul,
    string? Keterangan);
