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

// Endpoint untuk aplikasi Android (Asisten Lapangan). Login memakai /api/auth/login yang sama (JWT).
// Hak kirim = permission Create pada menu SNS_MOBILE.
//
//   GET  /api/sensus/mobile/master  -> unduh jenis sensus + parameter dan daftar sumber persil
//   GET  /api/sensus/mobile/persil?sumber=KLP&wilayah=  -> unduh persil (MyPAD / RSUPPKB) untuk offline
//   POST /api/sensus/mobile/submit  -> kirim satu sensus. ClientUuid dibuat di HP; kirim ulang dengan
//                                      ClientUuid yang sama aman (tidak dobel). Bila status REVISI, data diganti.
//   GET  /api/sensus/mobile/mine    -> status sensus milik petugas (untuk melihat catatan revisi/penolakan)
[ApiController]
[Route("api/sensus/mobile")]
[Authorize]
public class SensusMobileController(
    AppDbContext db,
    IPermissionService permissions,
    PersilReader persilReader,
    ILogger<SensusMobileController> logger
) : ControllerBase
{
    [HttpGet("master")]
    public async Task<IActionResult> GetMaster()
    {
        if (!await CanSubmit()) return Forbid();

        return Ok(new SnsMobileMasterDto(
            await SensusMasterController.QueryJenis(db, activeOnly: true),
            PersilReader.Sources.Values
                .Select(s => new SnsPersilSumberDto(s.Key, s.Label, s.Database, s.Komoditas))
                .ToList()));
    }

    // Persil aktif per sumber (opsional per wilayah) untuk disimpan offline di HP.
    [HttpGet("persil")]
    public async Task<IActionResult> GetPersil(string sumber, string? wilayah)
    {
        if (!await CanSubmit()) return Forbid();
        var source = PersilReader.Find(sumber);
        if (source == null) return BadRequest(new { message = "Sumber persil tidak dikenal." });

        try
        {
            var rows = await persilReader.ListAsync(source, wilayah);
            var terpotong = rows.Count > PersilReader.MaxRows;
            if (terpotong) rows.RemoveAt(rows.Count - 1);
            return Ok(new { sumber = source.Key, source.Komoditas, total = rows.Count, terpotong, rows });
        }
        catch (DbException error)
        {
            return SensusPersilController.SourceError(logger, source, error);
        }
    }

    [HttpPost("submit")]
    public async Task<IActionResult> Submit(SnsMobileSubmitRequest body)
    {
        if (!await CanSubmit()) return Forbid();
        if (body.ClientUuid == Guid.Empty) return BadRequest(new { message = "ClientUuid wajib diisi." });

        var userId = SensusHelper.CurrentUserId(User);

        var existing = await db.SnsHeaders
            .Include(x => x.Details)
            .Include(x => x.Fotos)
            .FirstOrDefaultAsync(x => x.ClientUuid == body.ClientUuid);

        if (existing != null)
        {
            if (existing.PetugasUserId != userId)
                return BadRequest(new { message = "ClientUuid sudah dipakai petugas lain." });

            // Sudah pernah terkirim & tidak diminta revisi -> anggap sukses (sync ulang dari HP).
            if (existing.Status != SnsStatus.Revisi)
                return Ok(new SnsMobileSubmitResponse(existing.Id, existing.SensusNo, existing.ClientUuid, existing.Status, true,
                    "Data sudah pernah dikirim."));
        }

        var jenis = await db.SnsJenis.AsNoTracking()
            .Include(x => x.Parameters)
            .FirstOrDefaultAsync(x => x.Active &&
                                      (body.JenisId != null ? x.Id == body.JenisId : x.Code == (body.JenisCode ?? "").Trim().ToUpper()));
        if (jenis == null) return BadRequest(new { message = "Jenis sensus tidak ditemukan / tidak aktif." });

        // Persil harus ada & aktif di MyPAD / RSUPPKB.
        var source = PersilReader.Find(body.PersilSumber);
        if (source == null) return BadRequest(new { message = "Sumber persil tidak dikenal (KLP, TNM_NNS, LHN_NNS, PNN)." });
        if (string.IsNullOrWhiteSpace(body.PersilId)) return BadRequest(new { message = "PersilId wajib diisi." });

        PersilReader.Persil? persil;
        try
        {
            persil = await persilReader.FindAsync(source, body.PersilId);
        }
        catch (DbException error)
        {
            return SensusPersilController.SourceError(logger, source, error);
        }

        if (persil == null) return BadRequest(new { message = $"Persil {body.PersilId} tidak ditemukan / tidak aktif di {source.Label}." });
        if (jenis.Komoditas != "SEMUA" && jenis.Komoditas != source.Komoditas)
            return BadRequest(new { message = $"Jenis sensus {jenis.Name} tidak berlaku untuk persil {source.Komoditas}." });

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        if (body.TanggalSensus == default || body.TanggalSensus > today)
            return BadRequest(new { message = "Tanggal sensus tidak valid." });
        if (body.JumlahPokokSampel < 0 || body.JumlahPokokTerserang < 0 || body.JumlahPokokTerserang > body.JumlahPokokSampel)
            return BadRequest(new { message = "Jumlah pokok terserang tidak boleh melebihi pokok sampel." });

        // Validasi & petakan detail ke parameter.
        var parameters = jenis.Parameters.Where(p => p.Active).ToList();
        var details = new List<SnsDetail>();

        foreach (var d in body.Details)
        {
            var p = d.ParameterId != null
                ? parameters.FirstOrDefault(x => x.Id == d.ParameterId)
                : parameters.FirstOrDefault(x => x.Code == (d.ParameterCode ?? "").Trim().ToUpperInvariant());

            if (p == null) return BadRequest(new { message = $"Parameter {d.ParameterId?.ToString() ?? d.ParameterCode} tidak dikenal untuk {jenis.Name}." });
            if (p.TipeData == "NUMBER" && d.NilaiAngka == null)
                return BadRequest(new { message = $"Parameter {p.Name} harus berisi angka." });
            if (p.TipeData == "OPTION" && !string.IsNullOrWhiteSpace(d.NilaiTeks) &&
                !(p.Opsi ?? "").Split(',').Select(x => x.Trim()).Contains(d.NilaiTeks.Trim(), StringComparer.OrdinalIgnoreCase))
                return BadRequest(new { message = $"Nilai {d.NilaiTeks} bukan pilihan parameter {p.Name}." });

            details.Add(new SnsDetail
            {
                ParameterId = p.Id,
                NoPokok = d.NoPokok,
                NilaiAngka = d.NilaiAngka,
                NilaiTeks = d.NilaiTeks?.Trim()
            });
        }

        var missing = parameters.Where(p => p.Wajib && details.All(d => d.ParameterId != p.Id)).Select(p => p.Name).ToList();
        if (missing.Count > 0) return BadRequest(new { message = $"Parameter wajib belum diisi: {string.Join(", ", missing)}." });

        var fotos = body.Fotos
            .Where(f => !string.IsNullOrWhiteSpace(f.FileUrl))
            .Select(f => new SnsFoto
            {
                FileUrl = f.FileUrl.Trim(),
                Keterangan = f.Keterangan?.Trim(),
                Latitude = f.Latitude,
                Longitude = f.Longitude,
                TakenAt = f.TakenAt?.ToUniversalTime()
            })
            .ToList();

        await using var tx = await db.Database.BeginTransactionAsync();

        var header = existing ?? new SnsHeader { ClientUuid = body.ClientUuid, PetugasUserId = userId };
        var fromStatus = existing?.Status;

        header.JenisId = jenis.Id;
        header.PersilSumber = source.Key;
        header.PersilId = persil.PersilId ?? body.PersilId.Trim();
        header.PersilKode = persil.Kode;
        header.KodeWilayah = persil.Wilayah;
        header.KodeAfdeling = persil.Afdeling;
        header.Komoditas = source.Komoditas;
        header.BlokId = null;
        header.TanggalSensus = body.TanggalSensus;
        header.JumlahPokokSampel = body.JumlahPokokSampel;
        header.JumlahPokokTerserang = body.JumlahPokokTerserang;
        header.Latitude = body.Latitude;
        header.Longitude = body.Longitude;
        header.Catatan = body.Catatan?.Trim();
        header.DeviceInfo = body.DeviceInfo?.Trim();
        header.Status = SnsStatus.Submitted;
        header.SubmittedAt = DateTime.UtcNow;

        if (existing != null)
        {
            db.SnsDetails.RemoveRange(existing.Details);
            db.SnsFotos.RemoveRange(existing.Fotos);
            header.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            db.SnsHeaders.Add(header);
        }

        await db.SaveChangesAsync();

        // Nomor sensus: SNS-yyMMdd-000123
        header.SensusNo ??= $"SNS-{DateTime.UtcNow.AddHours(7):yyMMdd}-{header.Id:D6}";

        details.ForEach(x => x.HeaderId = header.Id);
        fotos.ForEach(x => x.HeaderId = header.Id);
        db.SnsDetails.AddRange(details);
        db.SnsFotos.AddRange(fotos);

        db.SnsLogs.Add(new SnsLog
        {
            HeaderId = header.Id,
            UserId = userId,
            Action = existing == null ? "SUBMIT" : "RESUBMIT",
            FromStatus = fromStatus,
            ToStatus = SnsStatus.Submitted,
            Note = existing == null ? "Dikirim dari Android." : "Dikirim ulang setelah revisi."
        });

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new SnsMobileSubmitResponse(header.Id, header.SensusNo, header.ClientUuid, header.Status, false, "Sensus berhasil dikirim."));
    }

    [HttpGet("mine")]
    public async Task<IActionResult> GetMine(DateOnly? dateFrom)
    {
        if (!await CanSubmit()) return Forbid();

        var userId = SensusHelper.CurrentUserId(User);
        dateFrom ??= DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)).AddDays(-60);

        return Ok(await db.SnsHeaders.AsNoTracking()
            .Where(x => x.PetugasUserId == userId && x.TanggalSensus >= dateFrom)
            .OrderByDescending(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.SensusNo,
                x.ClientUuid,
                JenisName = x.Jenis!.Name,
                x.PersilSumber,
                x.PersilKode,
                x.KodeWilayah,
                x.TanggalSensus,
                x.Status,
                x.ApprovalNote,
                x.ApprovedAt
            })
            .ToListAsync());
    }

    private Task<bool> CanSubmit() => permissions.HasPermissionAsync(User, SnsMenu.Mobile, MenuPermission.Create);
}
