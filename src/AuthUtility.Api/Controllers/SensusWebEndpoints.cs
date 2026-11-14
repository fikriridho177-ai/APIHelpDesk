using System.Data;
using AuthUtility.Api.Sensus;
using AuthUtility.Application.DTOs.Sensus;
using AuthUtility.Application.Interfaces;
using AuthUtility.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Api.Controllers;

// Web extensions of the existing Sensus controller; no separate RNA data model.
public partial class SensusController
{
    private async Task<bool> CanEditPhotos(SnsHeader header) => SensusPhotoPolicy.CanEdit(
        SensusHelper.CurrentUserId(User), header.PetugasUserId, header.Status,
        await permissions.HasPermissionAsync(User, SnsMenu.Mobile, MenuPermission.Create));

    private async Task<SnsHeader?> PhotoHeader(long id, CancellationToken ct)
    {
        // Lock the same header approval updates, so status changes cannot race photo writes.
        if (db.Database.IsNpgsql())
            return (await db.SnsHeaders.FromSqlInterpolated($"SELECT * FROM \"SnsHeader\" WHERE \"Id\" = {id} FOR UPDATE").ToListAsync(ct)).SingleOrDefault();
        return await db.SnsHeaders.FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    [HttpPost("{id:long}/fotos")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> UploadFoto(long id, IFormFile file, [FromForm] string? keterangan,
        [FromServices] IWebHostEnvironment environment, CancellationToken ct)
    {
        if (file == null) return BadRequest(new { message = "Pilih berkas foto." });
        var signature = new byte[12];
        await using (var input = file.OpenReadStream())
        {
            var count = await input.ReadAtLeastAsync(signature, signature.Length, throwOnEndOfStream: false, cancellationToken: ct);
            var error = SensusPhotoPolicy.Validate(file.FileName, file.ContentType, file.Length, signature.AsSpan(0, count));
            if (error != null) return BadRequest(new { message = error });
        }
        if (keterangan?.Trim().Length > 200) return BadRequest(new { message = "Keterangan foto maksimal 200 karakter." });

        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var header = await PhotoHeader(id, ct);
        if (header == null) return NotFound();
        if (!await CanEditPhotos(header)) return Forbid();
        if (await db.SnsFotos.CountAsync(x => x.HeaderId == id, ct) >= SensusPhotoPolicy.MaxFiles)
            return BadRequest(new { message = "Maksimal 5 foto per sensus." });

        var date = DateTime.UtcNow.AddHours(7);
        var name = Guid.NewGuid().ToString("N") + Path.GetExtension(file.FileName).ToLowerInvariant();
        var url = $"/uploads/sensus/{date:yyyy}/{date:MM}/{id}/{name}";
        var path = SensusPhotoPolicy.OwnedPath(SensusPhotoPolicy.Root(environment), id, url)!;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await file.CopyToAsync(output, ct);
            if (new FileInfo(path).Length > SensusPhotoPolicy.MaxBytes)
            {
                System.IO.File.Delete(path);
                return BadRequest(new { message = "Foto maksimal 5 MB." });
            }
            var foto = new SnsFoto { HeaderId = id, FileUrl = url, Keterangan = keterangan?.Trim() };
            db.SnsFotos.Add(foto);
            await db.SaveChangesAsync(ct);
            if (transaction != null) await transaction.CommitAsync(ct);
            return Ok(new SnsFotoDto(foto.Id, foto.FileUrl, foto.Keterangan, null, null, null));
        }
        catch
        {
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            throw;
        }
    }

    [HttpDelete("{id:long}/fotos/{fotoId:long}")]
    public async Task<IActionResult> DeleteFoto(long id, long fotoId, [FromServices] IWebHostEnvironment environment, CancellationToken ct)
    {
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var header = await PhotoHeader(id, ct);
        if (header == null) return NotFound();
        if (!await CanEditPhotos(header)) return Forbid();
        var foto = await db.SnsFotos.FirstOrDefaultAsync(x => x.Id == fotoId && x.HeaderId == id, ct);
        if (foto == null) return NotFound();
        var path = SensusPhotoPolicy.OwnedPath(SensusPhotoPolicy.Root(environment), id, foto.FileUrl);
        db.SnsFotos.Remove(foto);
        await db.SaveChangesAsync(ct);
        if (transaction != null) await transaction.CommitAsync(ct);
        if (path != null)
        {
            try { System.IO.File.Delete(path); }
            catch (IOException error) { logger.LogWarning(error, "Berkas foto sensus {FotoId} tidak dapat dibersihkan", fotoId); }
            catch (UnauthorizedAccessException error) { logger.LogWarning(error, "Berkas foto sensus {FotoId} tidak dapat dibersihkan", fotoId); }
        }
        return NoContent();
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(int? jenisId, string? wilayah, DateOnly? dateFrom, DateOnly? dateTo, string sumber = "KLP", int hariTerlambat = 30)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        dateTo ??= today;
        dateFrom ??= new DateOnly(today.Year, today.Month, 1).AddMonths(-5);
        var result = await GetMonitoring(jenisId, wilayah, dateFrom, dateTo, hariTerlambat, sumber);
        if (result is not OkObjectResult { Value: SnsMonitoringDto monitoring }) return result;
        var headers = await Filter(db.SnsHeaders.AsNoTracking(), jenisId, wilayah, dateFrom, dateTo).Include(x => x.Jenis).ToListAsync();
        var approvedIds = headers.Where(x => x.Status == SnsStatus.Approved).Select(x => x.Id).ToList();
        var tingkat = await SensusHelper.HitungTingkatAsync(db, approvedIds);
        return Ok(SensusDashboardCalculator.Build(monitoring, headers, tingkat));
    }
}
