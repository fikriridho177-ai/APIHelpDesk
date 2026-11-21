using System.Data.Common;
using AuthUtility.Api.Sensus;
using AuthUtility.Application.DTOs.Sensus;
using AuthUtility.Application.Interfaces;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Api.Controllers;

// Sensus di web: Approval (SNS_APPROVAL), Monitoring (SNS_MONITORING), Analisis (SNS_ANALISIS).
// Data sensus masuk dari Android lewat SensusMobileController.
[ApiController]
[Route("api/sensus")]
[Authorize]
public partial class SensusController(
    AppDbContext db,
    IPermissionService permissions,
    PersilReader persilReader,
    ILogger<SensusController> logger
) : ControllerBase
{
    private const int MaxListSize = 1000;
    private const int MaxPersilTerlambat = 300;

    // Aksi approval -> status tujuan & catatan wajib.
    private static readonly Dictionary<string, (string To, bool NoteRequired)> Actions = new()
    {
        ["APPROVE"] = (SnsStatus.Approved, false),
        ["REJECT"] = (SnsStatus.Rejected, true),
        ["REVISI"] = (SnsStatus.Revisi, true),
    };

    /* =========================================================
       DAFTAR & DETAIL
    ========================================================= */

    [HttpGet]
    public async Task<IActionResult> GetList(
        string? status, int? jenisId, string? wilayah, string? afdeling, string? persilSumber, string? persilId, int? petugasUserId,
        DateOnly? dateFrom, DateOnly? dateTo, string? search)
    {
        // Halaman sensus per jenis (sub-layanan RNA) cukup punya Read di menu jenis tersebut.
        if (!await CanAny(MenuPermission.Read, SnsMenu.Approval, SnsMenu.Monitoring) &&
            !(jenisId != null && await CanJenis(jenisId.Value, MenuPermission.Read)))
            return Forbid();

        var query = Filter(db.SnsHeaders.AsNoTracking(), jenisId, wilayah, dateFrom, dateTo);

        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(afdeling)) query = query.Where(x => x.KodeAfdeling == afdeling);
        if (!string.IsNullOrWhiteSpace(persilSumber)) query = query.Where(x => x.PersilSumber == persilSumber);
        if (!string.IsNullOrWhiteSpace(persilId)) query = query.Where(x => x.PersilId == persilId);
        if (petugasUserId != null) query = query.Where(x => x.PetugasUserId == petugasUserId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = $"%{search.Trim()}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.SensusNo ?? "", keyword) ||
                EF.Functions.ILike(x.PersilKode, keyword) ||
                EF.Functions.ILike(x.PersilId, keyword) ||
                EF.Functions.ILike(x.Petugas!.Name, keyword));
        }

        var rows = await query
            .OrderByDescending(x => x.TanggalSensus).ThenByDescending(x => x.Id)
            .Take(MaxListSize)
            .Include(x => x.Jenis).ThenInclude(x=>x!.Rules)
            .Include(x => x.HptDetails).ThenInclude(x => x.Hpt).ThenInclude(x => x!.Rules)
            .Include(x => x.Details).ThenInclude(d=>d.Parameter)
            .Include(x => x.Petugas)
            .AsSplitQuery()
            .ToListAsync();

        var tingkat = await SensusHelper.HitungTingkatAsync(db, rows.Select(x => x.Id).ToList(),jenisId);
        var evaluation = await SensusHptEvaluation.EvaluateAsync(db,rows.Select(x=>x.Id).ToList(),jenisId);
        return Ok(rows.Select(x => ToListItem(ForListScope(x,jenisId), tingkat, evaluation)).ToList());
    }

    // Halaman sensus per jenis: sub-layanan RNA (mis. /layanan/rna/sensus-hpt/sensus-ulat-api)
    // dicari jenis sensusnya lewat SnsJenis.MenuCode. Akses = Hak Akses menu sub-layanan tersebut.
    [HttpGet("jenis-by-menu/{menuCode}")]
    public async Task<IActionResult> GetJenisByMenu(string menuCode)
    {
        var p = await permissions.GetPermissionsAsync(User, menuCode);
        if (p == null || !p.CanView) return Forbid();

        var jenis = (await SensusMasterController.QueryJenis(db, activeOnly: true))
            .FirstOrDefault(x => string.Equals(x.MenuCode, menuCode, StringComparison.OrdinalIgnoreCase));

        // null = menu ini bukan halaman sensus (tetap memakai form pengajuan layanan).
        if (jenis == null) return Ok(null);

        return Ok(new SnsJenisPageDto(
            jenis,
            p.CanRead || await CanAny(MenuPermission.Read, SnsMenu.Approval, SnsMenu.Monitoring),
            await CanApprove(jenis.Id),
            p.CanExport));
    }

    // Daftar wilayah yang ada di data sensus (untuk filter Approval / Monitoring / Analisis).
    [HttpGet("wilayah")]
    public async Task<IActionResult> GetWilayah()
    {
        if (!await SensusHelper.HasAnyAccessAsync(db, permissions, User)) return Forbid();

        return Ok(await db.SnsHeaders.AsNoTracking()
            .Where(x => x.KodeWilayah != null && x.KodeWilayah != "")
            .Select(x => x.KodeWilayah!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync());
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetDetail(long id)
    {
        var header = await db.SnsHeaders.AsNoTracking().Include(x=>x.HptDetails).FirstOrDefaultAsync(x=>x.Id==id);
        if (header == null) return NotFound(new { message = "Data sensus tidak ditemukan." });

        var ownerCanRead = await db.SnsHeaders.AnyAsync(x => x.Id == id && x.PetugasUserId == SensusHelper.CurrentUserId(User))
            && await permissions.HasPermissionAsync(User, SnsMenu.Mobile, MenuPermission.Create);
        HashSet<int>? visibleHpts = null;
        if (!ownerCanRead && !await CanAny(MenuPermission.Read, SnsMenu.Approval, SnsMenu.Monitoring, SnsMenu.Analisis))
        {
            visibleHpts = new();
            foreach(var hptId in HeaderHptIds(header))
                if(await CanJenis(hptId,MenuPermission.Read)) visibleHpts.Add(hptId);
            if(visibleHpts.Count==0) return Forbid();
        }

        var detail = await BuildDetail(id,visibleHpts);
        return detail == null ? NotFound(new { message = "Data sensus tidak ditemukan." }) : Ok(detail);
    }

    /* =========================================================
       APPROVAL
    ========================================================= */

    [HttpPost("{id:long}/actions")]
    public async Task<IActionResult> RunAction(long id, SnsActionRequest body)
    {
        var action = body.Action.Trim().ToUpperInvariant();
        if (!Actions.TryGetValue(action, out var transition)) return BadRequest(new { message = "Aksi tidak valid." });

        var note = body.Note?.Trim();
        if (transition.NoteRequired && string.IsNullOrWhiteSpace(note))
            return BadRequest(new { message = "Catatan wajib diisi untuk menolak / meminta revisi." });

        var header = await db.SnsHeaders.Include(x=>x.HptDetails).FirstOrDefaultAsync(x => x.Id == id);
        if (header == null) return NotFound(new { message = "Data sensus tidak ditemukan." });
        if (!await CanApproveHeader(header)) return NoAccess("menyetujui sensus");
        if (header.Status != SnsStatus.Submitted)
            return BadRequest(new { message = $"Sensus berstatus {header.Status} tidak dapat diproses lagi." });

        ApplyAction(header, action, transition.To, note);
        await db.SaveChangesAsync();

        return Ok(await BuildDetail(id));
    }

    // Setujui banyak sensus sekaligus (hanya yang berstatus SUBMITTED).
    [HttpPost("bulk-approve")]
    public async Task<IActionResult> BulkApprove(SnsBulkApproveRequest body)
    {
        if (body.Ids.Count == 0) return BadRequest(new { message = "Pilih minimal satu data sensus." });

        var headers = await db.SnsHeaders
            .Where(x => body.Ids.Contains(x.Id) && x.Status == SnsStatus.Submitted)
            .Include(x=>x.HptDetails)
            .ToListAsync();

        // Hanya sensus yang boleh di-approve user (Approval Sensus atau Approve di menu jenisnya).
        var allowed = new List<SnsHeader>();
        foreach(var header in headers)
            if(await CanApproveHeader(header)) allowed.Add(header);
        if(headers.Count>0 && allowed.Count==0) return NoAccess("menyetujui sensus");
        headers = allowed;

        foreach (var header in headers)
            ApplyAction(header, "APPROVE", SnsStatus.Approved, body.Note?.Trim());

        await db.SaveChangesAsync();
        return Ok(new { approved = headers.Count, skipped = body.Ids.Count - headers.Count });
    }

    /* =========================================================
       MONITORING
    ========================================================= */

    [HttpGet("monitoring")]
    public async Task<IActionResult> GetMonitoring(
        int? jenisId, string? wilayah, DateOnly? dateFrom, DateOnly? dateTo, int hariTerlambat = 30, string sumber = "KLP")
    {
        if (!await permissions.HasPermissionAsync(User, SnsMenu.Monitoring, MenuPermission.Read)) return Forbid();

        return await BuildMonitoring(jenisId, wilayah, dateFrom, dateTo, hariTerlambat, sumber);
    }

    private async Task<IActionResult> BuildMonitoring(int? jenisId, string? wilayah, DateOnly? dateFrom, DateOnly? dateTo, int hariTerlambat, string sumber)
    {

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        dateTo ??= today;
        dateFrom ??= dateTo.Value.AddDays(-29);

        var source = PersilReader.Find(sumber);
        if (source == null) return BadRequest(new { message = "Sumber persil tidak dikenal." });

        var monitoringHeaders = await Filter(db.SnsHeaders.AsNoTracking(), jenisId, wilayah, dateFrom, dateTo)
            .Include(x=>x.Jenis).Include(x=>x.Petugas).Include(x=>x.HptDetails).ThenInclude(d=>d.Hpt)
            .AsSplitQuery().ToListAsync();
        MonitoringRow Row(SnsHeader x)=>new(x.Status,x.TanggalSensus,x.JenisId??0,x.Jenis?.Name??"Seluruh HPT",
            x.PetugasUserId,x.Petugas?.Name??"",x.KodeWilayah??"(kosong)");
        var rows=monitoringHeaders.Select(Row).ToList();
        var perHpt=monitoringHeaders.SelectMany(SensusParcelHpt.Expand)
            .Where(x=>jenisId==null||x.JenisId==jenisId).Select(Row).ToList();

        int CountOf(string s) => rows.Count(x => x.Status == s);

        // Persil terlambat dibaca dari MyPAD / RSUPPKB; bila gagal, monitoring lain tetap tampil.
        List<SnsPersilTerlambatDto> terlambat = new();
        string? terlambatError = null;
        try
        {
            terlambat = await GetPersilTerlambat(source, jenisId, wilayah, today, Math.Max(1, hariTerlambat));
        }
        catch (DbException error)
        {
            logger.LogError(error, "Gagal membaca persil {Sumber} untuk monitoring", source.Key);
            terlambatError = $"Gagal membaca data persil dari database {source.Database}.";
        }

        // Key untuk per hari = yyyy-MM-dd agar urutan tanggal benar.
        var perHari = CountBy(rows, x => x.TanggalSensus.ToString("yyyy-MM-dd"), x => x.TanggalSensus.ToString("dd/MM"))
            .OrderBy(x => x.Key)
            .ToList();

        return Ok(new SnsMonitoringDto(
            rows.Count,
            CountOf(SnsStatus.Submitted),
            CountOf(SnsStatus.Approved),
            CountOf(SnsStatus.Rejected),
            CountOf(SnsStatus.Revisi),
            CountBy(perHpt, x => x.JenisId.ToString(), x => x.JenisName),
            CountBy(rows, x => x.PetugasUserId.ToString(), x => x.PetugasName),
            CountBy(rows, x => x.Wilayah, x => x.Wilayah),
            perHari,
            source.Key,
            terlambat,
            terlambatError));
    }

    // Status sensus seluruh persil aktif (belum / sudah / terlambat) dengan pencarian dan paging.
    [HttpGet("monitoring/persil")]
    public async Task<IActionResult> GetPersilStatus(
        int? jenisId, string? wilayah, string sumber = "KLP", int hariTerlambat = 30,
        string? search = null, string? status = null, int page = 1, int pageSize = 50)
    {
        if (!await permissions.HasPermissionAsync(User, SnsMenu.Monitoring, MenuPermission.Read)) return Forbid();

        var source = PersilReader.Find(sumber);
        if (source == null) return BadRequest(new { message = "Sumber persil tidak dikenal." });

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 200);
        var hari = Math.Max(1, hariTerlambat);
        var keyword = search?.Trim();
        var statusFilter = status?.Trim().ToUpperInvariant();

        try
        {
            var combos = await BuildPersilStatus(source, jenisId, wilayah, today, hari);
            bool Match(string? value) => value != null && keyword != null && value.Contains(keyword, StringComparison.OrdinalIgnoreCase);
            var filtered = combos.Where(x => string.IsNullOrEmpty(keyword)
                || Match(x.PersilKode) || Match(x.PersilId) || Match(x.KodeWilayah) || Match(x.KodeAfdeling) || Match(x.JenisName));
            if (statusFilter is "BELUM" or "SUDAH" or "TERLAMBAT") filtered = filtered.Where(x => x.Status == statusFilter);

            var total = filtered.Count();
            var rows = filtered
                .OrderBy(x => x.Status == "BELUM" ? 0 : x.Status == "TERLAMBAT" ? 1 : 2)
                .ThenBy(x => x.Status == "TERLAMBAT" ? -(x.HariSejak ?? 0) : (x.HariSejak ?? 0))
                .ThenBy(x => x.KodeWilayah).ThenBy(x => x.PersilKode).ThenBy(x => x.JenisName)
                .Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return Ok(new SnsPersilStatusResultDto(
                source.Key, total,
                combos.Count(x => x.Status == "BELUM"),
                combos.Count(x => x.Status == "SUDAH"),
                combos.Count(x => x.Status == "TERLAMBAT"),
                page, pageSize, rows, null));
        }
        catch (DbException error)
        {
            logger.LogError(error, "Gagal membaca status persil {Sumber}", source.Key);
            return Ok(new SnsPersilStatusResultDto(source.Key, 0, 0, 0, 0, page, pageSize, new(),
                $"Gagal membaca data persil dari database {source.Database}."));
        }
    }

    private async Task<List<SnsPersilStatusDto>> BuildPersilStatus(
        PersilReader.Source source, int? jenisId, string? wilayah, DateOnly today, int hari)
    {
        var jenisList = await db.SnsJenis.AsNoTracking()
            .Where(x => x.Active && (jenisId == null || x.Id == jenisId) && (x.Komoditas == "SEMUA" || x.Komoditas == source.Komoditas))
            .Select(x => new { x.Id, x.Name })
            .ToListAsync();
        if (jenisList.Count == 0) return new();

        var persilList = (await persilReader.ListAsync(source, wilayah))
            .Where(p => !string.IsNullOrWhiteSpace(p.PersilId))
            .Take(PersilReader.MaxRows)
            .ToList();

        var recentQuery = db.SnsHeaders.AsNoTracking()
            .Where(x => x.PersilSumber == source.Key && (x.Status == SnsStatus.Approved || x.Status == SnsStatus.Submitted))
            .Where(x => wilayah == null || x.KodeWilayah == wilayah);
        var child = await recentQuery.SelectMany(x => x.HptDetails.Select(d => new { x.PersilId, JenisId = d.HptId, x.TanggalSensus }))
            .Where(x => jenisId == null || x.JenisId == jenisId)
            .GroupBy(x => new { x.PersilId, x.JenisId })
            .Select(g => new { g.Key.PersilId, g.Key.JenisId, Last = g.Max(x => x.TanggalSensus), Count = g.Count() })
            .ToListAsync();
        var legacy = await recentQuery.Where(x => !x.HptDetails.Any() && x.JenisId != null && (jenisId == null || x.JenisId == jenisId))
            .GroupBy(x => new { x.PersilId, JenisId = x.JenisId!.Value })
            .Select(g => new { g.Key.PersilId, g.Key.JenisId, Last = g.Max(x => x.TanggalSensus), Count = g.Count() })
            .ToListAsync();
        var stats = child.Concat(legacy).GroupBy(x => (x.PersilId, x.JenisId))
            .ToDictionary(g => g.Key, g => (Last: g.Max(x => x.Last), Count: g.Sum(x => x.Count)));

        var batas = today.AddDays(-hari);
        return (
            from persil in persilList
            from jenis in jenisList
            let stat = stats.TryGetValue((persil.PersilId!, jenis.Id), out var s) ? (Last: (DateOnly?)s.Last, Count: s.Count) : (Last: (DateOnly?)null, Count: 0)
            let last = stat.Last
            select new SnsPersilStatusDto(
                source.Key, persil.PersilId!, persil.Kode, persil.Wilayah, persil.Afdeling, jenis.Id, jenis.Name,
                last, last == null ? null : today.DayNumber - last.Value.DayNumber, stat.Count,
                last == null ? "BELUM" : last < batas ? "TERLAMBAT" : "SUDAH"))
            .ToList();
    }

    /* =========================================================
       ANALISIS (hanya data APPROVED)
    ========================================================= */

    [HttpGet("analisis")]
    public async Task<IActionResult> GetAnalisis(int jenisId, string? wilayah, DateOnly? dateFrom, DateOnly? dateTo)
    {
        if (!await permissions.HasPermissionAsync(User, SnsMenu.Monitoring, MenuPermission.Read) &&
            !await permissions.HasPermissionAsync(User, SnsMenu.Analisis, MenuPermission.Read) &&
            !await CanJenis(jenisId, MenuPermission.Read))
            return Forbid();

        var jenis = await db.SnsJenis.AsNoTracking().Include(x => x.Parameters).FirstOrDefaultAsync(x => x.Id == jenisId);
        if (jenis == null) return NotFound(new { message = "Jenis sensus tidak ditemukan." });

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        dateTo ??= today;
        dateFrom ??= dateTo.Value.AddMonths(-6);

        // Indikator utama = parameter indikator dengan Id terkecil (paling awal dibuat).
        var indikator = jenis.Parameters.Where(p => p.IsIndikator && p.Active).OrderBy(p => p.Id).FirstOrDefault();

        var headers = await Filter(db.SnsHeaders.AsNoTracking(), jenisId, wilayah, dateFrom, dateTo)
            .Where(x => x.Status == SnsStatus.Approved)
            .Select(x => new
            {
                x.Id,
                x.PersilSumber,
                x.PersilId,
                x.PersilKode,
                x.KodeWilayah,
                x.KodeAfdeling,
                x.TanggalSensus,
                x.JumlahPokokSampel,
                JumlahPokokTerserang=x.HptDetails.Any(d=>d.HptId==jenisId)
                    ? x.HptDetails.Where(d=>d.HptId==jenisId).Select(d=>d.JumlahSerangan).First()
                    : x.JumlahPokokTerserang
            })
            .ToListAsync();

        var headerIds = headers.Select(x => x.Id).ToList();
        var evaluations = await SensusHptEvaluation.EvaluateAsync(db,headerIds,jenisId);
        var headerLevels = await SensusHelper.HitungTingkatAsync(db,headerIds,jenisId);
        var nilai = indikator == null || headerIds.Count == 0
            ? new Dictionary<long, decimal?>()
            : await db.SnsDetails.AsNoTracking()
                .Where(d => headerIds.Contains(d.HeaderId) && d.ParameterId == indikator.Id && d.NilaiAngka != null)
                .GroupBy(d => d.HeaderId)
                .Select(g => new { g.Key, Rata = g.Average(x => x.NilaiAngka) })
                .ToDictionaryAsync(x => x.Key, x => x.Rata);

        decimal? Rata(IEnumerable<long> ids)
        {
            var values = ids.Where(nilai.ContainsKey).Select(id => nilai[id]).Where(v => v != null).ToList();
            return values.Count == 0 ? null : Math.Round(values.Average()!.Value, 2);
        }

        string Tingkat(decimal? value) =>
            indikator == null ? SnsTingkat.Kosong : SnsTingkat.Hitung(value, indikator.AmbangSedang, indikator.AmbangBerat);

        static decimal Persen(int terserang, int sampel) =>
            sampel == 0 ? 0 : Math.Round(terserang * 100m / sampel, 2);

        var perPersil = headers
            .GroupBy(x => new { x.PersilSumber, x.PersilId })
            .Select(g =>
            {
                var first = g.First();
                var rata = Rata(g.Select(x => x.Id));
                var latestId=g.OrderByDescending(x=>x.TanggalSensus).ThenByDescending(x=>x.Id).First().Id;
                return new SnsAnalisisPersilDto(
                    first.PersilSumber, first.PersilId, first.PersilKode, first.KodeWilayah, first.KodeAfdeling,
                    g.Count(),
                    g.Max(x => x.TanggalSensus),
                    g.Sum(x => x.JumlahPokokSampel),
                    g.Sum(x => x.JumlahPokokTerserang),
                    Persen(g.Sum(x => x.JumlahPokokTerserang), g.Sum(x => x.JumlahPokokSampel)),
                    rata,
                    headerLevels.TryGetValue(latestId,out var level)?level.Tingkat:Tingkat(rata), evaluations.GetValueOrDefault(latestId));
            })
            .OrderByDescending(x => Array.IndexOf(SnsTingkat.Urutan, x.Tingkat))
            .ThenByDescending(x => x.RataIndikator ?? 0)
            .ThenByDescending(x => x.PersenTerserang)
            .ToList();

        var tren = headers
            .GroupBy(x => new { x.TanggalSensus.Year, x.TanggalSensus.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new SnsTrenDto(
                $"{g.Key.Year}-{g.Key.Month:D2}",
                g.Count(),
                Persen(g.Sum(x => x.JumlahPokokTerserang), g.Sum(x => x.JumlahPokokSampel)),
                Rata(g.Select(x => x.Id))))
            .ToList();

        // Sebaran tingkat serangan per sensus.
        var sebaran = SnsTingkat.Urutan.ToDictionary(t => t, _ => 0);
        foreach (var h in headers)
            sebaran[headerLevels.TryGetValue(h.Id,out var level)?level.Tingkat:Tingkat(nilai.GetValueOrDefault(h.Id))]++;

        return Ok(new SnsAnalisisDto(
            jenis.Id,
            jenis.Name,
            indikator?.Name,
            indikator?.Satuan,
            indikator?.AmbangSedang,
            indikator?.AmbangBerat,
            headers.Count,
            perPersil.Count,
            Persen(headers.Sum(x => x.JumlahPokokTerserang), headers.Sum(x => x.JumlahPokokSampel)),
            sebaran,
            perPersil,
            tren));
    }

    /* =========================================================
       HELPER
    ========================================================= */

    private record MonitoringRow(
        string Status, DateOnly TanggalSensus, int JenisId, string JenisName,
        int PetugasUserId, string PetugasName, string Wilayah);

    private static List<SnsCountDto> CountBy(
        IEnumerable<MonitoringRow> rows, Func<MonitoringRow, string> key, Func<MonitoringRow, string> label) =>
        rows.GroupBy(key)
            .Select(g => new SnsCountDto(
                g.Key,
                label(g.First()),
                g.Count(),
                g.Count(x => x.Status == SnsStatus.Submitted),
                g.Count(x => x.Status == SnsStatus.Approved),
                g.Count(x => x.Status == SnsStatus.Rejected),
                g.Count(x => x.Status == SnsStatus.Revisi)))
            .OrderByDescending(x => x.Total)
            .ToList();

    private static IQueryable<SnsHeader> Filter(IQueryable<SnsHeader> query, int? jenisId, string? wilayah, DateOnly? dateFrom, DateOnly? dateTo)
    {
        if (jenisId != null) query = query.Where(x => x.HptDetails.Any(d=>d.HptId==jenisId) || !x.HptDetails.Any() && x.JenisId == jenisId);
        if (!string.IsNullOrWhiteSpace(wilayah)) query = query.Where(x => x.KodeWilayah == wilayah);
        if (dateFrom != null) query = query.Where(x => x.TanggalSensus >= dateFrom);
        if (dateTo != null) query = query.Where(x => x.TanggalSensus <= dateTo);
        return query;
    }

    // Persil aktif (MyPAD / RSUPPKB) yang belum disensus (APPROVED / SUBMITTED) lebih dari N hari,
    // per jenis sensus yang sesuai komoditas sumber persil.
    private async Task<List<SnsPersilTerlambatDto>> GetPersilTerlambat(
        PersilReader.Source source, int? jenisId, string? wilayah, DateOnly today, int hari)
    {
        var jenisList = await db.SnsJenis.AsNoTracking()
            .Where(x => x.Active && (jenisId == null || x.Id == jenisId) && (x.Komoditas == "SEMUA" || x.Komoditas == source.Komoditas))
            .Select(x => new { x.Id, x.Name })
            .ToListAsync();

        if (jenisList.Count == 0) return new();

        var persilList = (await persilReader.ListAsync(source, wilayah))
            .Where(p => !string.IsNullOrWhiteSpace(p.PersilId))
            .Take(PersilReader.MaxRows)
            .ToList();

        var recentQuery = db.SnsHeaders.AsNoTracking()
            .Where(x => x.PersilSumber == source.Key && (x.Status == SnsStatus.Approved || x.Status == SnsStatus.Submitted))
            .Where(x => wilayah==null || x.KodeWilayah==wilayah);
        var childDates=await recentQuery.SelectMany(x=>x.HptDetails.Select(d=>new {x.PersilId,JenisId=d.HptId,x.TanggalSensus}))
            .Where(x=>jenisId==null||x.JenisId==jenisId)
            .GroupBy(x => new { x.PersilId, x.JenisId })
            .Select(g => new { g.Key.PersilId, g.Key.JenisId, Last = g.Max(x => x.TanggalSensus) })
            .ToListAsync();
        var legacyDates=await recentQuery.Where(x=>!x.HptDetails.Any() && x.JenisId!=null && (jenisId==null||x.JenisId==jenisId))
            .GroupBy(x=>new{x.PersilId,JenisId=x.JenisId!.Value})
            .Select(g=>new{g.Key.PersilId,g.Key.JenisId,Last=g.Max(x=>x.TanggalSensus)}).ToListAsync();
        var terakhir=childDates.Concat(legacyDates).GroupBy(x=>(x.PersilId,x.JenisId)).ToDictionary(g=>g.Key,g=>g.Max(x=>x.Last));

        var batas = today.AddDays(-hari);

        return (
                from persil in persilList
                from jenis in jenisList
                let last = terakhir.TryGetValue((persil.PersilId!, jenis.Id), out var d) ? d : (DateOnly?)null
                where last == null || last < batas
                select new SnsPersilTerlambatDto(
                    source.Key, persil.PersilId!, persil.Kode, persil.Wilayah, persil.Afdeling, jenis.Id, jenis.Name,
                    last, last == null ? null : today.DayNumber - last.Value.DayNumber))
            .OrderBy(x => x.TerakhirSensus.HasValue).ThenByDescending(x => x.HariSejak)
            .Take(MaxPersilTerlambat)
            .ToList();
    }

    private void ApplyAction(SnsHeader header, string action, string toStatus, string? note)
    {
        var userId = SensusHelper.CurrentUserId(User);
        var fromStatus = header.Status;

        header.Status = toStatus;
        header.ApprovalNote = note;
        header.UpdatedAt = DateTime.UtcNow;

        if (toStatus == SnsStatus.Approved || toStatus == SnsStatus.Rejected)
        {
            header.ApprovedByUserId = userId;
            header.ApprovedAt = DateTime.UtcNow;
        }

        db.SnsLogs.Add(new SnsLog
        {
            HeaderId = header.Id,
            UserId = userId,
            Action = action,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            Note = note
        });
    }

    private static SnsListItemDto ToListItem(SnsHeader x, Dictionary<long, (string Tingkat, decimal? Rata)> tingkat,
        IReadOnlyDictionary<long,SnsEvaluationDto>? evaluations = null) =>
        new(
            x.Id,
            x.SensusNo,
            x.JenisId ?? 0,
            x.Jenis?.Name ?? string.Join(", ",x.HptDetails.Select(d=>d.Hpt?.Name ?? d.HptId.ToString())),
            x.Jenis?.Kategori ?? "HPT",
            x.PersilSumber,
            x.PersilId,
            x.PersilKode,
            x.KodeWilayah,
            x.KodeAfdeling,
            x.Komoditas,
            x.TanggalSensus,
            x.PetugasUserId,
            x.Petugas?.Name ?? "",
            x.JumlahPokokSampel,
            x.JumlahPokokTerserang,
            x.Status,
            tingkat.TryGetValue(x.Id, out var t) ? t.Tingkat : SnsTingkat.Kosong,
            x.SubmittedAt,
            x.ApprovedAt,
            evaluations?.GetValueOrDefault(x.Id),SensusBusinessContext.Dto(x),SensusParcelHpt.Dtos(x));

    private async Task<SnsDetailDto?> BuildDetail(long id,HashSet<int>? visibleHpts=null)
    {
        var header = await db.SnsHeaders.AsNoTracking()
            .Include(x => x.Jenis).ThenInclude(x => x!.Rules)
            .Include(x => x.HptDetails).ThenInclude(x => x.Hpt).ThenInclude(x=>x!.Rules)
            .Include(x => x.Petugas)
            .Include(x => x.ApprovedBy)
            .Include(x => x.Details).ThenInclude(x => x.Parameter)
            .Include(x => x.Fotos)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (header == null) return null;

        var canApprove = header.Status == SnsStatus.Submitted && await CanApproveHeader(header);
        if(visibleHpts!=null)
        {
            header.HptDetails=header.HptDetails.Where(d=>visibleHpts.Contains(d.HptId)).ToList();
            header.Details=header.Details.Where(d=>d.Parameter!=null && visibleHpts.Contains(d.Parameter.JenisId)).ToList();
            if(header.JenisId.HasValue && !visibleHpts.Contains(header.JenisId.Value)) {header.JenisId=null;header.Jenis=null;}
            if(header.HptDetails.Count==1) header=ForListScope(header,header.HptDetails.Single().HptId);
        }

        var tingkat = new Dictionary<long,(string Tingkat,decimal? Rata)>{{id,SensusParcelHpt.Level(header)}};

        var details = header.Details
            .OrderBy(x => x.NoPokok ?? 0).ThenBy(x => x.ParameterId)
            .Select(x => new SnsDetailValueDto(
                x.Id, x.ParameterId, x.Parameter?.Name ?? "", x.Parameter?.Satuan, x.Parameter?.TipeData ?? "",
                x.Parameter?.IsIndikator ?? false, x.NoPokok, x.NilaiAngka, x.NilaiTeks))
            .ToList();

        var ringkasan = header.Details
            .GroupBy(x => x.ParameterId)
            .Select(g =>
            {
                var p = g.First().Parameter;
                var numbers = g.Where(x => x.NilaiAngka != null).Select(x => x.NilaiAngka!.Value).ToList();
                decimal? rata = numbers.Count == 0 ? null : Math.Round(numbers.Average(), 2);
                return new
                {
                    Sort = g.Key,
                    Dto = new SnsParameterSummaryDto(
                        g.Key, p?.Name ?? "", p?.Satuan, p?.IsIndikator ?? false,
                        rata,
                        numbers.Count == 0 ? null : numbers.Sum(),
                        g.Count(),
                        p?.IsIndikator == true ? SnsTingkat.Hitung(rata, p.AmbangSedang, p.AmbangBerat) : SnsTingkat.Kosong)
                };
            })
            .OrderBy(x => x.Sort)
            .Select(x => x.Dto)
            .ToList();

        var logs = await db.SnsLogs.AsNoTracking()
            .Where(x => x.HeaderId == id)
            .OrderBy(x => x.Id)
            .Select(x => new SnsLogDto(x.Id, x.Action, x.FromStatus, x.ToStatus, x.Note, x.User!.Name, x.CreatedAt))
            .ToListAsync();

        return new SnsDetailDto(
            ToListItem(header, tingkat, new Dictionary<long,SnsEvaluationDto>{{header.Id,SensusHptEvaluation.Evaluate(header)}}),
            header.Latitude,
            header.Longitude,
            header.Catatan,
            header.DeviceInfo,
            header.ApprovedBy?.Name,
            header.ApprovalNote,
            canApprove,
            ringkasan,
            details,
            header.Fotos.OrderBy(x => x.Id)
                .Select(x => new SnsFotoDto(x.Id, x.FileUrl, x.Keterangan, x.Latitude, x.Longitude, x.TakenAt))
                .ToList(),
            logs,
            await CanEditPhotos(header),
            ownerCanAccessUuid(header) ? header.ClientUuid : null,
            (await SensusMasterController.QueryJenis(db,activeOnly:false)).Where(k=>HeaderHptIds(header).Contains(k.Id)).ToList());
    }

    private bool ownerCanAccessUuid(SnsHeader header) => header.PetugasUserId == SensusHelper.CurrentUserId(User);

    private Task<bool> CanJenis(int jenisId, MenuPermission permission) =>
        SensusHelper.HasJenisPermissionAsync(db, permissions, User, jenisId, permission);

    private async Task<bool> CanApprove(int jenisId) =>
        await permissions.HasPermissionAsync(User, SnsMenu.Approval, MenuPermission.Approve) ||
        await CanJenis(jenisId, MenuPermission.Approve);

    private static IEnumerable<int> HeaderHptIds(SnsHeader header) => header.HptDetails.Count>0
        ? header.HptDetails.Select(d=>d.HptId).Distinct()
        : header.JenisId.HasValue ? new[]{header.JenisId.Value} : Array.Empty<int>();

    private async Task<bool> CanApproveHeader(SnsHeader header)
    {
        if(await permissions.HasPermissionAsync(User,SnsMenu.Approval,MenuPermission.Approve)) return true;
        var ids=HeaderHptIds(header).ToList();
        if(ids.Count==0) return false;
        foreach(var id in ids) if(!await CanJenis(id,MenuPermission.Approve)) return false;
        return true;
    }

    private static SnsHeader ForListScope(SnsHeader header,int? hptId)
    {
        if(hptId==null || header.HptDetails.Count==0) return header;
        var child=header.HptDetails.FirstOrDefault(d=>d.HptId==hptId);
        if(child==null) return header;
        var scoped=SensusParcelHpt.ForHpt(header,child);
        scoped.HptDetails=new List<SnsHptDetail>{child};
        return scoped;
    }

    private async Task<bool> CanAny(MenuPermission permission, params string[] menuCodes)
    {
        foreach (var code in menuCodes)
            if (await permissions.HasPermissionAsync(User, code, permission)) return true;
        return false;
    }

    private ObjectResult NoAccess(string action) =>
        StatusCode(StatusCodes.Status403Forbidden, new { message = $"Anda tidak memiliki akses untuk {action}." });
}
