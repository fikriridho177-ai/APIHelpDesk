using AuthUtility.Application.DTOs.Qc;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Api.Qc;

// Validasi master QC; pesan dibaca user lewat response BadRequest.
public class QcValidationException(string message) : Exception(message);

// CRUD master QC + versi form + alur approval. Semua tulis hanya ke helpdeskasd.
public class QcMasterService(AppDbContext db)
{
    private static readonly string[] Commodities = { "KELAPA", "NANAS" };
    private static readonly string[] Patterns = { "A", "B", "C", "D", "E" };

    // ---------- Form & versi ----------

    public async Task<IReadOnlyList<QcFormMasterDto>> ListFormsAsync(string? search, bool includeInactive, CancellationToken ct = default)
    {
        var q = db.QcForms.AsNoTracking().AsQueryable();
        if (!includeInactive) q = q.Where(x => x.Active);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(x => x.Code.Contains(s) || x.Name.Contains(s) || (x.ExcelCode != null && x.ExcelCode.Contains(s)));
        }
        var counts = await db.QcInspections.AsNoTracking()
            .GroupBy(x => x.FormId)
            .Select(g => new { FormId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.FormId, x => x.Count, ct);
        var forms = await q.OrderBy(x => x.Code).ThenByDescending(x => x.Version).ToListAsync(ct);
        return forms.Select(x => ToDto(x, counts.GetValueOrDefault(x.Id))).ToList();
    }

    public async Task<QcFormMasterDto> CreateFormAsync(QcFormSaveDto dto, string userId, CancellationToken ct = default)
    {
        var code = Require(dto.Code, "Kode form");
        var name = Require(dto.Name, "Nama form");
        var commodity = NormalizeChoice(dto.Commodity, Commodities, "Komoditas")!;
        var pattern = NormalizeChoice(dto.Pattern, Patterns, "Pola", nullable: true);
        var versions = await db.QcForms.Where(x => x.Code == code).Select(x => x.Version).ToListAsync(ct);
        var form = new QcForm
        {
            Code = code, Name = name, Commodity = commodity, Description = Trim(dto.Description),
            Pattern = pattern, TemplateFile = Trim(dto.TemplateFile), PrintTitle = Trim(dto.PrintTitle) ?? name,
            ExcelCode = Trim(dto.ExcelCode), EffectiveFrom = dto.EffectiveFrom,
            Version = versions.Count == 0 ? 1 : versions.Max() + 1,
            Active = true, CreatedBy = userId, CreatedAt = DateTime.UtcNow
        };
        db.QcForms.Add(form);
        await db.SaveChangesAsync(ct);
        return ToDto(form, 0);
    }

    public async Task<QcFormMasterDto> UpdateFormAsync(int id, QcFormSaveDto dto, string userId, CancellationToken ct = default)
    {
        var form = await db.QcForms.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new QcValidationException("Form QC tidak ditemukan.");
        if (!string.Equals(form.Code, dto.Code?.Trim(), StringComparison.Ordinal))
            throw new QcValidationException("Kode form tidak boleh diubah; buat versi baru bila perlu.");
        form.Name = Require(dto.Name, "Nama form");
        form.Commodity = NormalizeChoice(dto.Commodity, Commodities, "Komoditas")!;
        form.Description = Trim(dto.Description);
        form.Pattern = NormalizeChoice(dto.Pattern, Patterns, "Pola", nullable: true);
        form.TemplateFile = Trim(dto.TemplateFile);
        form.PrintTitle = Trim(dto.PrintTitle) ?? form.Name;
        form.ExcelCode = Trim(dto.ExcelCode);
        form.EffectiveFrom = dto.EffectiveFrom;
        form.UpdatedBy = userId;
        form.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        var count = await db.QcInspections.CountAsync(x => x.FormId == id, ct);
        return ToDto(form, count);
    }

    public Task SetFormActiveAsync(int id, bool active, string userId, CancellationToken ct = default) =>
        SetActiveAsync(db.QcForms, id, active, userId, "Form QC", ct);

    // ---------- Alur approval ----------

    public async Task<IReadOnlyList<QcApprovalStepDto>> ListStepsAsync(int? formId, CancellationToken ct = default)
    {
        var q = db.QcFormApprovalSteps.AsNoTracking().AsQueryable();
        if (formId != null) q = q.Where(x => x.FormId == formId);
        var steps = await q.OrderBy(x => x.FormId).ThenBy(x => x.Level).ToListAsync(ct);
        var groupIds = steps.Where(x => x.ApproverGroupId != null).Select(x => x.ApproverGroupId!.Value).Distinct().ToList();
        var groups = groupIds.Count == 0
            ? new Dictionary<int, string>()
            : await db.Groups.AsNoTracking().Where(g => groupIds.Contains(g.Id))
                .ToDictionaryAsync(g => g.Id, g => g.Name, ct);
        return steps.Select(x => new QcApprovalStepDto(
            x.Id, x.FormId, x.Level, x.Label, x.JabatanKode, x.ApproverGroupId,
            x.ApproverGroupId == null ? null : groups.GetValueOrDefault(x.ApproverGroupId.Value),
            x.MenuCode, x.ScopeByAfdeling, x.IsCreatorStep, x.Active)).ToList();
    }

    public async Task<QcApprovalStepDto> CreateStepAsync(QcApprovalStepSaveDto dto, string userId, CancellationToken ct = default)
    {
        _ = await db.QcForms.AnyAsync(x => x.Id == dto.FormId, ct)
            ? 0 : throw new QcValidationException("Form QC tidak ditemukan.");
        ValidateLevel(dto.Level);
        if (dto.IsCreatorStep && dto.Level != 1)
            throw new QcValidationException("Langkah pembuat (Dilaporkan) hanya boleh di level 1.");
        if (await db.QcFormApprovalSteps.AnyAsync(x => x.FormId == dto.FormId && x.Level == dto.Level, ct))
            throw new QcValidationException($"Level {dto.Level} sudah ada untuk form ini.");
        var step = new QcFormApprovalStep
        {
            FormId = dto.FormId, Level = dto.Level, Label = Require(dto.Label, "Label approval"),
            JabatanKode = Trim(dto.JabatanKode), ApproverGroupId = dto.ApproverGroupId,
            MenuCode = Trim(dto.MenuCode) ?? "QC_APPROVAL", ScopeByAfdeling = dto.ScopeByAfdeling,
            IsCreatorStep = dto.IsCreatorStep,
            Active = true, CreatedBy = userId, CreatedAt = DateTime.UtcNow
        };
        await ValidateGroupAsync(step.ApproverGroupId, ct);
        db.QcFormApprovalSteps.Add(step);
        await db.SaveChangesAsync(ct);
        return (await ListStepsAsync(step.FormId, ct)).First(x => x.Id == step.Id);
    }

    public async Task<QcApprovalStepDto> UpdateStepAsync(int id, QcApprovalStepSaveDto dto, string userId, CancellationToken ct = default)
    {
        var step = await db.QcFormApprovalSteps.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new QcValidationException("Langkah approval tidak ditemukan.");
        ValidateLevel(dto.Level);
        if (dto.IsCreatorStep && dto.Level != 1)
            throw new QcValidationException("Langkah pembuat (Dilaporkan) hanya boleh di level 1.");
        if (step.Level != dto.Level &&
            await db.QcFormApprovalSteps.AnyAsync(x => x.FormId == step.FormId && x.Level == dto.Level && x.Id != id, ct))
            throw new QcValidationException($"Level {dto.Level} sudah ada untuk form ini.");
        step.Level = dto.Level;
        step.Label = Require(dto.Label, "Label approval");
        step.JabatanKode = Trim(dto.JabatanKode);
        step.ApproverGroupId = dto.ApproverGroupId;
        step.MenuCode = Trim(dto.MenuCode) ?? "QC_APPROVAL";
        step.ScopeByAfdeling = dto.ScopeByAfdeling;
        step.IsCreatorStep = dto.IsCreatorStep;
        step.UpdatedBy = userId;
        step.UpdatedAt = DateTime.UtcNow;
        await ValidateGroupAsync(step.ApproverGroupId, ct);
        await db.SaveChangesAsync(ct);
        return (await ListStepsAsync(step.FormId, ct)).First(x => x.Id == step.Id);
    }

    public Task SetStepActiveAsync(int id, bool active, string userId, CancellationToken ct = default) =>
        SetActiveAsync(db.QcFormApprovalSteps, id, active, userId, "Langkah approval", ct);

    // ---------- Cache persil (read-only terhadap sumber) ----------

    public async Task<QcPersilPageDto> ListPersilAsync(
        string? sumber, string? wilayah, string? komoditas, string? search, bool includeInactive,
        int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var q = db.QcPersils.AsNoTracking().AsQueryable();
        if (!includeInactive) q = q.Where(x => x.Active);
        if (!string.IsNullOrWhiteSpace(sumber)) q = q.Where(x => x.Sumber == sumber.Trim().ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(wilayah)) q = q.Where(x => x.WilayahKode == wilayah.Trim());
        if (!string.IsNullOrWhiteSpace(komoditas)) q = q.Where(x => x.Komoditas == komoditas.Trim().ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(x => x.PersilKode.Contains(s) || x.PersilId.Contains(s)
                || (x.NamaPersil != null && x.NamaPersil.Contains(s))
                || (x.WilayahKode != null && x.WilayahKode.Contains(s))
                || (x.KodeAfdeling != null && x.KodeAfdeling.Contains(s)));
        }
        var total = await q.CountAsync(ct);
        var rows = await q.OrderBy(x => x.WilayahKode).ThenBy(x => x.PersilKode)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var lastSync = await db.QcPersils.MaxAsync(x => (DateTime?)x.SyncedAt, ct);
        var perSumber = await db.QcPersils.AsNoTracking()
            .GroupBy(x => x.Sumber)
            .Select(g => new QcPersilSumberCount(g.Key, g.Count(), g.Count(x => x.Active)))
            .ToListAsync(ct);
        return new QcPersilPageDto(rows.Select(ToDto).ToList(), total, page, pageSize, lastSync, perSumber);
    }

    // ---------- Pemborong ----------

    public async Task<IReadOnlyList<QcPemborongDto>> ListPemborongAsync(string? search, bool includeInactive, CancellationToken ct = default)
    {
        var q = db.QcPemborong.AsNoTracking().AsQueryable();
        if (!includeInactive) q = q.Where(x => x.Active);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(x => x.Kode.Contains(s) || x.Name.Contains(s));
        }
        return (await q.OrderBy(x => x.Kode).ToListAsync(ct))
            .Select(x => new QcPemborongDto(x.Id, x.Kode, x.Name, x.WilayahKode, x.Sumber, x.Active)).ToList();
    }

    public async Task<QcPemborongDto> CreatePemborongAsync(QcPemborongSaveDto dto, string userId, CancellationToken ct = default)
    {
        var kode = Require(dto.Kode, "Kode pemborong");
        if (await db.QcPemborong.AnyAsync(x => x.Kode == kode, ct))
            throw new QcValidationException($"Kode pemborong {kode} sudah dipakai.");
        var row = new QcPemborong
        {
            Kode = kode, Name = Require(dto.Name, "Nama pemborong"), WilayahKode = Trim(dto.WilayahKode),
            Sumber = "LOCAL", Active = true, CreatedBy = userId, CreatedAt = DateTime.UtcNow
        };
        db.QcPemborong.Add(row);
        await db.SaveChangesAsync(ct);
        return new QcPemborongDto(row.Id, row.Kode, row.Name, row.WilayahKode, row.Sumber, row.Active);
    }

    public async Task<QcPemborongDto> UpdatePemborongAsync(int id, QcPemborongSaveDto dto, string userId, CancellationToken ct = default)
    {
        var row = await db.QcPemborong.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new QcValidationException("Pemborong tidak ditemukan.");
        var kode = Require(dto.Kode, "Kode pemborong");
        if (await db.QcPemborong.AnyAsync(x => x.Kode == kode && x.Id != id, ct))
            throw new QcValidationException($"Kode pemborong {kode} sudah dipakai.");
        row.Kode = kode;
        row.Name = Require(dto.Name, "Nama pemborong");
        row.WilayahKode = Trim(dto.WilayahKode);
        row.UpdatedBy = userId;
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return new QcPemborongDto(row.Id, row.Kode, row.Name, row.WilayahKode, row.Sumber, row.Active);
    }

    public Task SetPemborongActiveAsync(int id, bool active, string userId, CancellationToken ct = default) =>
        SetActiveAsync(db.QcPemborong, id, active, userId, "Pemborong", ct);

    // ---------- Mandor ----------

    public async Task<IReadOnlyList<QcMandorDto>> ListMandorAsync(string? search, bool includeInactive, CancellationToken ct = default)
    {
        var q = db.QcMandors.AsNoTracking().Include(x => x.Pemborong).AsQueryable();
        if (!includeInactive) q = q.Where(x => x.Active);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(x => x.Name.Contains(s)
                || (x.KodeAfdeling != null && x.KodeAfdeling.Contains(s))
                || (x.WilayahKode != null && x.WilayahKode.Contains(s)));
        }
        return (await q.OrderBy(x => x.Name).ToListAsync(ct))
            .Select(x => new QcMandorDto(x.Id, x.Name, x.WilayahKode, x.KodeAfdeling, x.PemborongId, x.Pemborong?.Name, x.Active)).ToList();
    }

    public async Task<QcMandorDto> CreateMandorAsync(QcMandorSaveDto dto, string userId, CancellationToken ct = default)
    {
        await ValidatePemborongAsync(dto.PemborongId, ct);
        var row = new QcMandor
        {
            Name = Require(dto.Name, "Nama mandor"), WilayahKode = Trim(dto.WilayahKode),
            KodeAfdeling = Trim(dto.KodeAfdeling), PemborongId = dto.PemborongId,
            Active = true, CreatedBy = userId, CreatedAt = DateTime.UtcNow
        };
        db.QcMandors.Add(row);
        await db.SaveChangesAsync(ct);
        return (await ListMandorAsync(null, true, ct)).First(x => x.Id == row.Id);
    }

    public async Task<QcMandorDto> UpdateMandorAsync(int id, QcMandorSaveDto dto, string userId, CancellationToken ct = default)
    {
        var row = await db.QcMandors.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new QcValidationException("Mandor tidak ditemukan.");
        await ValidatePemborongAsync(dto.PemborongId, ct);
        row.Name = Require(dto.Name, "Nama mandor");
        row.WilayahKode = Trim(dto.WilayahKode);
        row.KodeAfdeling = Trim(dto.KodeAfdeling);
        row.PemborongId = dto.PemborongId;
        row.UpdatedBy = userId;
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return (await ListMandorAsync(null, true, ct)).First(x => x.Id == row.Id);
    }

    public Task SetMandorActiveAsync(int id, bool active, string userId, CancellationToken ct = default) =>
        SetActiveAsync(db.QcMandors, id, active, userId, "Mandor", ct);

    // ---------- Jenis pupuk ----------

    public async Task<IReadOnlyList<QcJenisPupukDto>> ListJenisPupukAsync(string? search, bool includeInactive, CancellationToken ct = default)
    {
        var q = db.QcJenisPupuk.AsNoTracking().AsQueryable();
        if (!includeInactive) q = q.Where(x => x.Active);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(x => x.Kode.Contains(s) || x.Name.Contains(s));
        }
        return (await q.OrderBy(x => x.Kode).ToListAsync(ct))
            .Select(x => new QcJenisPupukDto(x.Id, x.Kode, x.Name, x.Satuan, x.DosisJadwalGram, x.Komoditas, x.Active)).ToList();
    }

    public async Task<QcJenisPupukDto> CreateJenisPupukAsync(QcJenisPupukSaveDto dto, string userId, CancellationToken ct = default)
    {
        var kode = Require(dto.Kode, "Kode jenis pupuk").ToUpperInvariant();
        if (await db.QcJenisPupuk.AnyAsync(x => x.Kode == kode, ct))
            throw new QcValidationException($"Kode jenis pupuk {kode} sudah dipakai.");
        var row = new QcJenisPupuk
        {
            Kode = kode, Name = Require(dto.Name, "Nama jenis pupuk"),
            Satuan = Trim(dto.Satuan) ?? "gram", DosisJadwalGram = dto.DosisJadwalGram,
            Komoditas = Trim(dto.Komoditas),
            Active = true, CreatedBy = userId, CreatedAt = DateTime.UtcNow
        };
        db.QcJenisPupuk.Add(row);
        await db.SaveChangesAsync(ct);
        return new QcJenisPupukDto(row.Id, row.Kode, row.Name, row.Satuan, row.DosisJadwalGram, row.Komoditas, row.Active);
    }

    public async Task<QcJenisPupukDto> UpdateJenisPupukAsync(int id, QcJenisPupukSaveDto dto, string userId, CancellationToken ct = default)
    {
        var row = await db.QcJenisPupuk.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new QcValidationException("Jenis pupuk tidak ditemukan.");
        var kode = Require(dto.Kode, "Kode jenis pupuk").ToUpperInvariant();
        if (await db.QcJenisPupuk.AnyAsync(x => x.Kode == kode && x.Id != id, ct))
            throw new QcValidationException($"Kode jenis pupuk {kode} sudah dipakai.");
        row.Kode = kode;
        row.Name = Require(dto.Name, "Nama jenis pupuk");
        row.Satuan = Trim(dto.Satuan) ?? "gram";
        row.DosisJadwalGram = dto.DosisJadwalGram;
        row.Komoditas = Trim(dto.Komoditas);
        row.UpdatedBy = userId;
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return new QcJenisPupukDto(row.Id, row.Kode, row.Name, row.Satuan, row.DosisJadwalGram, row.Komoditas, row.Active);
    }

    public Task SetJenisPupukActiveAsync(int id, bool active, string userId, CancellationToken ct = default) =>
        SetActiveAsync(db.QcJenisPupuk, id, active, userId, "Jenis pupuk", ct);

    // ---------- Grade ----------

    public async Task<IReadOnlyList<QcGradeDto>> ListGradeAsync(string? search, bool includeInactive, CancellationToken ct = default)
    {
        var q = db.QcGrades.AsNoTracking().AsQueryable();
        if (!includeInactive) q = q.Where(x => x.Active);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(x => x.Kode.Contains(s) || x.Name.Contains(s));
        }
        return (await q.OrderBy(x => x.Urutan).ThenBy(x => x.Kode).ToListAsync(ct))
            .Select(x => new QcGradeDto(x.Id, x.Kode, x.Name, x.Komoditas, x.Urutan, x.Active)).ToList();
    }

    public async Task<QcGradeDto> CreateGradeAsync(QcGradeSaveDto dto, string userId, CancellationToken ct = default)
    {
        var kode = Require(dto.Kode, "Kode grade").ToUpperInvariant();
        if (await db.QcGrades.AnyAsync(x => x.Kode == kode, ct))
            throw new QcValidationException($"Kode grade {kode} sudah dipakai.");
        var row = new QcGrade
        {
            Kode = kode, Name = Require(dto.Name, "Nama grade"), Komoditas = Trim(dto.Komoditas),
            Urutan = dto.Urutan, Active = true, CreatedBy = userId, CreatedAt = DateTime.UtcNow
        };
        db.QcGrades.Add(row);
        await db.SaveChangesAsync(ct);
        return new QcGradeDto(row.Id, row.Kode, row.Name, row.Komoditas, row.Urutan, row.Active);
    }

    public async Task<QcGradeDto> UpdateGradeAsync(int id, QcGradeSaveDto dto, string userId, CancellationToken ct = default)
    {
        var row = await db.QcGrades.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new QcValidationException("Grade tidak ditemukan.");
        var kode = Require(dto.Kode, "Kode grade").ToUpperInvariant();
        if (await db.QcGrades.AnyAsync(x => x.Kode == kode && x.Id != id, ct))
            throw new QcValidationException($"Kode grade {kode} sudah dipakai.");
        row.Kode = kode;
        row.Name = Require(dto.Name, "Nama grade");
        row.Komoditas = Trim(dto.Komoditas);
        row.Urutan = dto.Urutan;
        row.UpdatedBy = userId;
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return new QcGradeDto(row.Id, row.Kode, row.Name, row.Komoditas, row.Urutan, row.Active);
    }

    public Task SetGradeActiveAsync(int id, bool active, string userId, CancellationToken ct = default) =>
        SetActiveAsync(db.QcGrades, id, active, userId, "Grade", ct);

    // ---------- Standar ----------

    public async Task<IReadOnlyList<QcStandardDto>> ListStandardAsync(int? formId, string? search, bool includeInactive, CancellationToken ct = default)
    {
        var q = db.QcStandards.AsNoTracking().Include(x => x.Form).AsQueryable();
        if (!includeInactive) q = q.Where(x => x.Active);
        if (formId != null) q = q.Where(x => x.FormId == formId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(x => x.ParameterKey.Contains(s) || (x.Keterangan != null && x.Keterangan.Contains(s)));
        }
        return (await q.OrderBy(x => x.FormId).ThenBy(x => x.ParameterKey).ToListAsync(ct))
            .Select(x => new QcStandardDto(x.Id, x.FormId, x.Form?.Code ?? "", x.ParameterKey, x.TargetPercent, x.Operator, x.Keterangan, x.Active)).ToList();
    }

    public async Task<QcStandardDto> CreateStandardAsync(QcStandardSaveDto dto, string userId, CancellationToken ct = default)
    {
        await ValidateStandardAsync(dto, null, ct);
        var row = new QcStandard
        {
            FormId = dto.FormId, ParameterKey = Require(dto.ParameterKey, "Parameter"),
            TargetPercent = dto.TargetPercent, Operator = NormalizeOperator(dto.Operator),
            Keterangan = Trim(dto.Keterangan), Active = true, CreatedBy = userId, CreatedAt = DateTime.UtcNow
        };
        db.QcStandards.Add(row);
        await db.SaveChangesAsync(ct);
        return (await ListStandardAsync(row.FormId, null, true, ct)).First(x => x.Id == row.Id);
    }

    public async Task<QcStandardDto> UpdateStandardAsync(int id, QcStandardSaveDto dto, string userId, CancellationToken ct = default)
    {
        var row = await db.QcStandards.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new QcValidationException("Standar tidak ditemukan.");
        await ValidateStandardAsync(dto, id, ct);
        row.FormId = dto.FormId;
        row.ParameterKey = Require(dto.ParameterKey, "Parameter");
        row.TargetPercent = dto.TargetPercent;
        row.Operator = NormalizeOperator(dto.Operator);
        row.Keterangan = Trim(dto.Keterangan);
        row.UpdatedBy = userId;
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return (await ListStandardAsync(row.FormId, null, true, ct)).First(x => x.Id == row.Id);
    }

    public Task SetStandardActiveAsync(int id, bool active, string userId, CancellationToken ct = default) =>
        SetActiveAsync(db.QcStandards, id, active, userId, "Standar", ct);

    // ---------- Helper ----------

    private async Task ValidateStandardAsync(QcStandardSaveDto dto, int? exceptId, CancellationToken ct)
    {
        if (!await db.QcForms.AnyAsync(x => x.Id == dto.FormId, ct))
            throw new QcValidationException("Form QC tidak ditemukan.");
        if (dto.TargetPercent is < 0 or > 100)
            throw new QcValidationException("Target persen harus antara 0 dan 100.");
        var key = Require(dto.ParameterKey, "Parameter");
        if (await db.QcStandards.AnyAsync(x => x.FormId == dto.FormId && x.ParameterKey == key && x.Id != exceptId, ct))
            throw new QcValidationException($"Parameter {key} sudah punya standar untuk form ini.");
    }

    private static string NormalizeOperator(string? op)
    {
        var v = (op ?? "MAX").Trim().ToUpperInvariant();
        return v is "MAX" or "MIN" ? v : throw new QcValidationException("Operator standar harus MAX atau MIN.");
    }

    private async Task ValidateGroupAsync(int? groupId, CancellationToken ct)
    {
        if (groupId != null && !await db.Groups.AnyAsync(x => x.Id == groupId, ct))
            throw new QcValidationException("Group approver tidak ditemukan.");
    }

    private async Task ValidatePemborongAsync(int? pemborongId, CancellationToken ct)
    {
        if (pemborongId != null && !await db.QcPemborong.AnyAsync(x => x.Id == pemborongId, ct))
            throw new QcValidationException("Pemborong tidak ditemukan.");
    }

    private static void ValidateLevel(int level)
    {
        if (level is < 1 or > 8) throw new QcValidationException("Level approval harus antara 1 dan 8.");
    }

    private async Task SetActiveAsync<T>(IQueryable<T> set, int id, bool active, string userId, string label, CancellationToken ct)
        where T : class
    {
        var row = await set.FirstOrDefaultAsync(x => EF.Property<int>(x, "Id") == id, ct)
            ?? throw new QcValidationException($"{label} tidak ditemukan.");
        var entry = db.Entry(row);
        entry.Property("Active").CurrentValue = active;
        entry.Property("UpdatedBy").CurrentValue = userId;
        entry.Property("UpdatedAt").CurrentValue = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private static QcFormMasterDto ToDto(QcForm x, int inspectionCount) =>
        new(x.Id, x.Code, x.Name, x.Commodity, x.Description, x.Active, x.Version, x.EffectiveFrom,
            x.Pattern, x.TemplateFile, x.PrintTitle, x.ExcelCode, inspectionCount, x.CreatedAt, x.UpdatedAt);

    private static QcPersilDto ToDto(QcPersil x) =>
        new(x.Id, x.Sumber, x.PersilId, x.PersilKode, x.NamaPersil, x.Komoditas, x.WilayahKode,
            x.KodeDivisi, x.KodeAfdeling, x.Kcb, x.BulanTanam, x.TahunTanam, x.LuasHa, x.JumlahJalur,
            x.JumlahPohon, x.PemborongKode, x.PemborongNama, x.Active, x.SyncedAt);

    private static string Require(string? value, string label)
    {
        var v = value?.Trim();
        if (string.IsNullOrEmpty(v)) throw new QcValidationException($"{label} wajib diisi.");
        return v;
    }

    private static string? Trim(string? value)
    {
        var v = value?.Trim();
        return string.IsNullOrEmpty(v) ? null : v;
    }

    private static string? NormalizeChoice(string? value, string[] allowed, string label, bool nullable = false)
    {
        var v = value?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(v))
        {
            if (nullable) return null;
            throw new QcValidationException($"{label} wajib diisi.");
        }
        if (!allowed.Contains(v)) throw new QcValidationException($"{label} harus salah satu dari: {string.Join(", ", allowed)}.");
        return v;
    }
}