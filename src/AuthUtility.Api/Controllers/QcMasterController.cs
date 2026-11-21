using System.Security.Claims;
using AuthUtility.Api.Qc;
using AuthUtility.Application.DTOs.Qc;
using AuthUtility.Application.Interfaces;
using AuthUtility.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthUtility.Api.Controllers;

// Master QC Fase 1: form & versi, alur approval, cache persil (read-only + sync),
// pemborong, mandor, jenis pupuk, grade, standar. Hak akses lewat menu QC_MASTER.
[ApiController]
[Route("api/qc/master")]
[Authorize]
public class QcMasterController(AppDbContext db, QcMasterService master, QcPersilSyncService sync) : ControllerBase
{
    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private string CurrentLogin => User.FindFirstValue(ClaimTypes.Name) ?? CurrentUserId.ToString();

    private async Task<QcAccess?> AccessAsync() => await QcAccess.LoadAsync(db, CurrentUserId);

    private static async Task<IActionResult> GuardAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (QcValidationException e) { return new BadRequestObjectResult(new { message = e.Message }); }
    }

    // ---------- Form & versi ----------

    [HttpGet("forms")]
    public async Task<IActionResult> Forms(string? search, bool includeInactive = false, CancellationToken ct = default)
    {
        var access = await AccessAsync();
        if (access == null || !access.Has(QcAccess.MenuMaster, MenuPermission.View)) return Forbid();
        return Ok(await master.ListFormsAsync(search, includeInactive, ct));
    }

    [HttpPost("forms")]
    public async Task<IActionResult> CreateForm([FromBody] QcFormSaveDto dto, CancellationToken ct)
    {
        var access = await AccessAsync();
        if (access == null || !access.Has(QcAccess.MenuMaster, MenuPermission.Create)) return Forbid();
        return await GuardAsync(async () => Ok(await master.CreateFormAsync(dto, CurrentLogin, ct)));
    }

    [HttpPut("forms/{id:int}")]
    public async Task<IActionResult> UpdateForm(int id, [FromBody] QcFormSaveDto dto, CancellationToken ct)
    {
        var access = await AccessAsync();
        if (access == null || !access.Has(QcAccess.MenuMaster, MenuPermission.Update)) return Forbid();
        return await GuardAsync(async () => Ok(await master.UpdateFormAsync(id, dto, CurrentLogin, ct)));
    }

    [HttpPut("forms/{id:int}/active")]
    public async Task<IActionResult> FormActive(int id, [FromBody] QcActiveDto dto, CancellationToken ct)
    {
        var access = await AccessAsync();
        if (access == null || !access.Has(QcAccess.MenuMaster, MenuPermission.Update)) return Forbid();
        return await GuardAsync(async () =>
        {
            await master.SetFormActiveAsync(id, dto.Active, CurrentLogin, ct);
            return Ok(new { message = dto.Active ? "Form diaktifkan." : "Form dinonaktifkan." });
        });
    }

    // ---------- Alur approval ----------

    [HttpGet("approval-steps")]
    public async Task<IActionResult> Steps(int? formId, CancellationToken ct)
    {
        var access = await AccessAsync();
        if (access == null || !access.Has(QcAccess.MenuMaster, MenuPermission.View)) return Forbid();
        return Ok(await master.ListStepsAsync(formId, ct));
    }

    [HttpPost("approval-steps")]
    public async Task<IActionResult> CreateStep([FromBody] QcApprovalStepSaveDto dto, CancellationToken ct)
    {
        var access = await AccessAsync();
        if (access == null || !access.Has(QcAccess.MenuMaster, MenuPermission.Create)) return Forbid();
        return await GuardAsync(async () => Ok(await master.CreateStepAsync(dto, CurrentLogin, ct)));
    }

    [HttpPut("approval-steps/{id:int}")]
    public async Task<IActionResult> UpdateStep(int id, [FromBody] QcApprovalStepSaveDto dto, CancellationToken ct)
    {
        var access = await AccessAsync();
        if (access == null || !access.Has(QcAccess.MenuMaster, MenuPermission.Update)) return Forbid();
        return await GuardAsync(async () => Ok(await master.UpdateStepAsync(id, dto, CurrentLogin, ct)));
    }

    [HttpPut("approval-steps/{id:int}/active")]
    public async Task<IActionResult> StepActive(int id, [FromBody] QcActiveDto dto, CancellationToken ct)
    {
        var access = await AccessAsync();
        if (access == null || !access.Has(QcAccess.MenuMaster, MenuPermission.Update)) return Forbid();
        return await GuardAsync(async () =>
        {
            await master.SetStepActiveAsync(id, dto.Active, CurrentLogin, ct);
            return Ok(new { message = dto.Active ? "Langkah approval diaktifkan." : "Langkah approval dinonaktifkan." });
        });
    }

    // ---------- Cache persil ----------

    [HttpGet("persil")]
    public async Task<IActionResult> Persil(
        string? sumber, string? wilayah, string? komoditas, string? search,
        bool includeInactive = false, int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var access = await AccessAsync();
        if (access == null || !access.Has(QcAccess.MenuMaster, MenuPermission.View)) return Forbid();
        return Ok(await master.ListPersilAsync(sumber, wilayah, komoditas, search, includeInactive, page, pageSize, ct));
    }

    [HttpPost("persil/sync")]
    public async Task<IActionResult> SyncPersil([FromBody] QcSyncRequestDto? dto, CancellationToken ct)
    {
        var access = await AccessAsync();
        if (access == null || !access.Has(QcAccess.MenuMaster, MenuPermission.Update)) return Forbid();
        try
        {
            var results = await sync.SyncAsync(dto?.Sumber, CurrentLogin, ct);
            return Ok(new { message = "Sync persil selesai.", results });
        }
        catch (ArgumentException e) { return BadRequest(new { message = e.Message }); }
        catch (Exception e) when (e is Microsoft.Data.SqlClient.SqlException or System.Data.Common.DbException)
        {
            return StatusCode(502, new { message = "Gagal membaca database sumber persil. Coba lagi beberapa saat." });
        }
    }

    // ---------- Pemborong / mandor / jenis pupuk / grade / standar ----------

    [HttpGet("pemborong")]
    public async Task<IActionResult> Pemborong(string? search, bool includeInactive = false, CancellationToken ct = default)
    {
        var access = await AccessAsync();
        if (access == null || !access.Has(QcAccess.MenuMaster, MenuPermission.View)) return Forbid();
        return Ok(await master.ListPemborongAsync(search, includeInactive, ct));
    }

    [HttpPost("pemborong")]
    public async Task<IActionResult> CreatePemborong([FromBody] QcPemborongSaveDto dto, CancellationToken ct)
    {
        var access = await AccessAsync();
        if (access == null || !access.Has(QcAccess.MenuMaster, MenuPermission.Create)) return Forbid();
        return await GuardAsync(async () => Ok(await master.CreatePemborongAsync(dto, CurrentLogin, ct)));
    }

    [HttpPut("pemborong/{id:int}")]
    public async Task<IActionResult> UpdatePemborong(int id, [FromBody] QcPemborongSaveDto dto, CancellationToken ct)
    {
        var access = await AccessAsync();
        if (access == null || !access.Has(QcAccess.MenuMaster, MenuPermission.Update)) return Forbid();
        return await GuardAsync(async () => Ok(await master.UpdatePemborongAsync(id, dto, CurrentLogin, ct)));
    }

    [HttpPut("pemborong/{id:int}/active")]
    public async Task<IActionResult> PemborongActive(int id, [FromBody] QcActiveDto dto, CancellationToken ct)
    {
        var access = await AccessAsync();
        if (access == null || !access.Has(QcAccess.MenuMaster, MenuPermission.Update)) return Forbid();
        return await GuardAsync(async () =>
        {
            await master.SetPemborongActiveAsync(id, dto.Active, CurrentLogin, ct);
            return Ok(new { message = dto.Active ? "Pemborong diaktifkan." : "Pemborong dinonaktifkan." });
        });
    }

    [HttpGet("mandor")]
    public async Task<IActionResult> Mandor(string? search, bool includeInactive = false, CancellationToken ct = default)
    {
        var access = await AccessAsync();
        if (access == null || !access.Has(QcAccess.MenuMaster, MenuPermission.View)) return Forbid();
        return Ok(await master.ListMandorAsync(search, includeInactive, ct));
    }

    [HttpPost("mandor")]
    public async Task<IActionResult> CreateMandor([FromBody] QcMandorSaveDto dto, CancellationToken ct)
    {
        var access = await AccessAsync();
        if (access == null || !access.Has(QcAccess.MenuMaster, MenuPermission.Create)) return Forbid();
        return await GuardAsync(async () => Ok(await master.CreateMandorAsync(dto, CurrentLogin, ct)));
    }

    [HttpPut("mandor/{id:int}")]
    public async Task<IActionResult> UpdateMandor(int id, [FromBody] QcMandorSaveDto dto, CancellationToken ct)
    {
        var access = await AccessAsync();
        if (access == null || !access.Has(QcAccess.MenuMaster, MenuPermission.Update)) return Forbid();
        return await GuardAsync(async () => Ok(await master.UpdateMandorAsync(id, dto, CurrentLogin, ct)));
    }

    [HttpPut("mandor/{id:int}/active")]
    public async Task<IActionResult> MandorActive(int id, [FromBody] QcActiveDto dto, CancellationToken ct)
    {
        var access = await AccessAsync();
        if (access == null || !access.Has(QcAccess.MenuMaster, MenuPermission.Update)) return Forbid();
        return await GuardAsync(async () =>
        {
            await master.SetMandorActiveAsync(id, dto.Active, CurrentLogin, ct);
            return Ok(new { message = dto.Active ? "Mandor diaktifkan." : "Mandor dinonaktifkan." });
        });
    }

    [HttpGet("jenis-pupuk")]
    public async Task<IActionResult> JenisPupuk(string? search, bool includeInactive = false, CancellationToken ct = default)
    {
        var access = await AccessAsync();
        if (access == null || !access.Has(QcAccess.MenuMaster, MenuPermission.View)) return Forbid();
        return Ok(await master.ListJenisPupukAsync(search, includeInactive, ct));
    }

    [HttpPost("jenis-pupuk")]
    public async Task<IActionResult> CreateJenisPupuk([FromBody] QcJenisPupukSaveDto dto, CancellationToken ct)
    {
        var access = await AccessAsync();
        if (access == null || !access.Has(QcAccess.MenuMaster, MenuPermission.Create)) return Forbid();
        return await GuardAsync(async () => Ok(await master.CreateJenisPupukAsync(dto, CurrentLogin, ct)));
    }

    [HttpPut("jenis-pupuk/{id:int}")]
    public async Task<IActionResult> UpdateJenisPupuk(int id, [FromBody] QcJenisPupukSaveDto dto, CancellationToken ct)
    {
        var access = await AccessAsync();
        if (access == null || !access.Has(QcAccess.MenuMaster, MenuPermission.Update)) return Forbid();
        return await GuardAsync(async () => Ok(await master.UpdateJenisPupukAsync(id, dto, CurrentLogin, ct)));
    }

    [HttpPut("jenis-pupuk/{id:int}/active")]
    public async Task<IActionResult> JenisPupukActive(int id, [FromBody] QcActiveDto dto, CancellationToken ct)
    {
        var access = await AccessAsync();
        if (access == null || !access.Has(QcAccess.MenuMaster, MenuPermission.Update)) return Forbid();
        return await GuardAsync(async () =>
        {
            await master.SetJenisPupukActiveAsync(id, dto.Active, CurrentLogin, ct);
            return Ok(new { message = dto.Active ? "Jenis pupuk diaktifkan." : "Jenis pupuk dinonaktifkan." });
        });
    }

    [HttpGet("grade")]
    public async Task<IActionResult> Grade(string? search, bool includeInactive = false, CancellationToken ct = default)
    {
        var access = await AccessAsync();
        if (access == null || !access.Has(QcAccess.MenuMaster, MenuPermission.View)) return Forbid();
        return Ok(await master.ListGradeAsync(search, includeInactive, ct));
    }

    [HttpPost("grade")]
    public async Task<IActionResult> CreateGrade([FromBody] QcGradeSaveDto dto, CancellationToken ct)
    {
        var access = await AccessAsync();
        if (access == null || !access.Has(QcAccess.MenuMaster, MenuPermission.Create)) return Forbid();
        return await GuardAsync(async () => Ok(await master.CreateGradeAsync(dto, CurrentLogin, ct)));
    }

    [HttpPut("grade/{id:int}")]
    public async Task<IActionResult> UpdateGrade(int id, [FromBody] QcGradeSaveDto dto, CancellationToken ct)
    {
        var access = await AccessAsync();
        if (access == null || !access.Has(QcAccess.MenuMaster, MenuPermission.Update)) return Forbid();
        return await GuardAsync(async () => Ok(await master.UpdateGradeAsync(id, dto, CurrentLogin, ct)));
    }

    [HttpPut("grade/{id:int}/active")]
    public async Task<IActionResult> GradeActive(int id, [FromBody] QcActiveDto dto, CancellationToken ct)
    {
        var access = await AccessAsync();
        if (access == null || !access.Has(QcAccess.MenuMaster, MenuPermission.Update)) return Forbid();
        return await GuardAsync(async () =>
        {
            await master.SetGradeActiveAsync(id, dto.Active, CurrentLogin, ct);
            return Ok(new { message = dto.Active ? "Grade diaktifkan." : "Grade dinonaktifkan." });
        });
    }

    [HttpGet("standard")]
    public async Task<IActionResult> Standard(int? formId, string? search, bool includeInactive = false, CancellationToken ct = default)
    {
        var access = await AccessAsync();
        if (access == null || !access.Has(QcAccess.MenuMaster, MenuPermission.View)) return Forbid();
        return Ok(await master.ListStandardAsync(formId, search, includeInactive, ct));
    }

    [HttpPost("standard")]
    public async Task<IActionResult> CreateStandard([FromBody] QcStandardSaveDto dto, CancellationToken ct)
    {
        var access = await AccessAsync();
        if (access == null || !access.Has(QcAccess.MenuMaster, MenuPermission.Create)) return Forbid();
        return await GuardAsync(async () => Ok(await master.CreateStandardAsync(dto, CurrentLogin, ct)));
    }

    [HttpPut("standard/{id:int}")]
    public async Task<IActionResult> UpdateStandard(int id, [FromBody] QcStandardSaveDto dto, CancellationToken ct)
    {
        var access = await AccessAsync();
        if (access == null || !access.Has(QcAccess.MenuMaster, MenuPermission.Update)) return Forbid();
        return await GuardAsync(async () => Ok(await master.UpdateStandardAsync(id, dto, CurrentLogin, ct)));
    }

    [HttpPut("standard/{id:int}/active")]
    public async Task<IActionResult> StandardActive(int id, [FromBody] QcActiveDto dto, CancellationToken ct)
    {
        var access = await AccessAsync();
        if (access == null || !access.Has(QcAccess.MenuMaster, MenuPermission.Update)) return Forbid();
        return await GuardAsync(async () =>
        {
            await master.SetStandardActiveAsync(id, dto.Active, CurrentLogin, ct);
            return Ok(new { message = dto.Active ? "Standar diaktifkan." : "Standar dinonaktifkan." });
        });
    }
}

public record QcSyncRequestDto(string? Sumber);