using AuthUtility.Api.Qc;
using AuthUtility.Api.Sensus;
using AuthUtility.Application.DTOs.Qc;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Tests;

// Fase 1: sync cache persil (idempotent), versi form, CRUD master & validasi.
public class QcMasterTests
{
    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed class FakeSource(Dictionary<string, List<QcPersilRaw>> data) : IQcPersilSourceReader
    {
        public Dictionary<string, List<QcPersilRaw>> Data { get; } = data;

        public Task<IReadOnlyList<QcPersilRaw>> ReadAsync(string sumber, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<QcPersilRaw>>(Data.TryGetValue(sumber, out var rows) ? rows : new List<QcPersilRaw>());
    }

    private static QcPersilRaw Raw(string id, string kode, string? pemborongKode = null, string? pemborongNama = null) =>
        new("KLP", id, kode, null, "KELAPA", "KIN1", "KIN-1", "KI-1", "14,5", 10, 2016, 3.75m, 12, 307,
            pemborongKode, pemborongNama);

    [Fact]
    public async Task SyncInsertsThenSecondRunReportsNoChange()
    {
        await using var db = NewDb();
        var source = new FakeSource(new() { ["KLP"] = new() { Raw("1", "A"), Raw("2", "B") } });
        var sync = new QcPersilSyncService(db, source);

        var first = await sync.SyncAsync("KLP", "admin");
        var r1 = Assert.Single(first);
        Assert.Equal(2, r1.Baru);
        Assert.Equal(0, r1.Diperbarui);

        var second = await sync.SyncAsync("KLP", "admin");
        var r2 = Assert.Single(second);
        Assert.Equal(0, r2.Baru);
        Assert.Equal(0, r2.Diperbarui);
        Assert.Equal(0, r2.Dinonaktifkan);
        Assert.Equal(2, await db.QcPersils.CountAsync());
    }

    [Fact]
    public async Task SyncUpdatesChangedFieldsAndDeactivatesMissingPersil()
    {
        await using var db = NewDb();
        var source = new FakeSource(new() { ["KLP"] = new() { Raw("1", "A"), Raw("2", "B") } });
        var sync = new QcPersilSyncService(db, source);
        await sync.SyncAsync("KLP", "admin");

        source.Data["KLP"] = new() { Raw("1", "A-REV") };
        var result = Assert.Single(await sync.SyncAsync("KLP", "admin"));
        Assert.Equal(1, result.Diperbarui);
        Assert.Equal(1, result.Dinonaktifkan);

        var changed = await db.QcPersils.SingleAsync(x => x.PersilId == "1");
        Assert.Equal("A-REV", changed.PersilKode);
        Assert.True(changed.Active);
        var gone = await db.QcPersils.SingleAsync(x => x.PersilId == "2");
        Assert.False(gone.Active);
    }

    [Fact]
    public async Task SyncCachesPemborongFromSourceWithoutOverwritingLocalEdit()
    {
        await using var db = NewDb();
        db.QcPemborong.Add(new QcPemborong { Kode = "MNGKI1", Name = "Editan User", Sumber = "LOCAL" });
        await db.SaveChangesAsync();
        var source = new FakeSource(new()
        {
            ["KLP"] = new() { Raw("1", "A", "MNGKI1", "Sumber Nama"), Raw("2", "B", "BRU01", "Pemborong Baru") }
        });
        await new QcPersilSyncService(db, source).SyncAsync("KLP", "admin");

        var local = await db.QcPemborong.SingleAsync(x => x.Kode == "MNGKI1");
        Assert.Equal("Editan User", local.Name);
        var synced = await db.QcPemborong.SingleAsync(x => x.Kode == "BRU01");
        Assert.Equal("Pemborong Baru", synced.Name);
        Assert.Equal("SYNC", synced.Sumber);
    }

    [Fact]
    public void MapRowParsesCommaDecimalsAndPlantingDatePerSource()
    {
        var klp = PersilReader.Sources["KLP"];
        var row = new PersilReader.Persil("KLP", "A", "KI-1", "KIN1", null, null, "1261", "A",
            new Dictionary<string, object?>
            {
                ["PersilID"] = 1261, ["KodeSubPersil"] = "A", ["KodeWilayah"] = "KIN1",
                ["KodeAfdeling"] = "KI-1", ["KodeDivisi"] = "KIN-1", ["KCB"] = "",
                ["LuasLahanKelapa"] = "3,7500", ["JumlahBaris"] = 0, ["Jumlah_Jalur"] = 12,
                ["JumlahPohonKelapa"] = 307, ["TglTanamKelapa"] = new DateTime(2016, 10, 1),
                ["KodePemborong"] = "MNGKI1", ["Pemborong"] = "SRTO"
            });
        var mapped = QcPersilSourceReader.MapRow(klp, row)!;
        Assert.Equal("1261", mapped.PersilId);
        Assert.Equal(3.75m, mapped.LuasHa);
        Assert.Equal(12, mapped.JumlahJalur);
        Assert.Equal(307, mapped.JumlahPohon);
        Assert.Equal(10, mapped.BulanTanam);
        Assert.Equal(2016, mapped.TahunTanam);
        Assert.Null(mapped.Kcb);
        Assert.Equal("MNGKI1", mapped.PemborongKode);

        var tnm = PersilReader.Sources["TNM_NNS"];
        var nns = new PersilReader.Persil("TNM_NNS", "TM-19-10AA", null, "RSTM", null, null, "1020", "TM-19-10AA",
            new Dictionary<string, object?>
            {
                ["PersilID"] = 1020, ["Kode"] = "TM-19-10AA", ["KodeWilayah"] = "RSTM", ["KCB"] = "14,5",
                ["TglTanam"] = new DateTime(2016, 1, 1), ["HA"] = "0,00", ["LuasLahan"] = "2,50",
                ["Jalur"] = "8,00", ["JumlahTanaman"] = 0, ["SisaTanaman"] = 4800
            });
        var mappedNns = QcPersilSourceReader.MapRow(tnm, nns)!;
        Assert.Equal("NANAS", mappedNns.Komoditas);
        Assert.Equal(0m, mappedNns.LuasHa);
        Assert.Equal(8, mappedNns.JumlahJalur);
        Assert.Equal(4800, mappedNns.JumlahPohon);
        Assert.Equal("14,5", mappedNns.Kcb);
        Assert.Null(mappedNns.KodeAfdeling);
    }

    [Fact]
    public async Task FormCreateMakesNewVersionAndListReturnsLatestFirst()
    {
        await using var db = NewDb();
        var master = new QcMasterService(db);
        var v1 = await master.CreateFormAsync(new QcFormSaveDto(
            "QC-CONTOH", "Contoh", "NANAS", null, "A", null, null, null, null), "admin");
        var v2 = await master.CreateFormAsync(new QcFormSaveDto(
            "QC-CONTOH", "Contoh revisi", "NANAS", null, "A", null, null, null, null), "admin");
        Assert.Equal(1, v1.Version);
        Assert.Equal(2, v2.Version);

        var list = await master.ListFormsAsync(null, false);
        Assert.Equal(2, list.Count);
        Assert.Equal(2, list[0].Version);
    }

    [Fact]
    public async Task FormUpdateRejectsCodeChangeAndSoftDeleteHidesRow()
    {
        await using var db = NewDb();
        var master = new QcMasterService(db);
        var form = await master.CreateFormAsync(new QcFormSaveDto(
            "QC-CONTOH", "Contoh", "NANAS", null, "A", null, null, null, null), "admin");

        var ex = await Assert.ThrowsAsync<QcValidationException>(() => master.UpdateFormAsync(form.Id,
            new QcFormSaveDto("QC-LAIN", "Contoh", "NANAS", null, "A", null, null, null, null), "admin"));
        Assert.Contains("Kode form", ex.Message);

        await master.SetFormActiveAsync(form.Id, false, "admin");
        Assert.Empty(await master.ListFormsAsync(null, false));
        Assert.Single(await master.ListFormsAsync(null, true));
    }

    [Fact]
    public async Task FormValidationRejectsUnknownCommodityAndPattern()
    {
        await using var db = NewDb();
        var master = new QcMasterService(db);
        await Assert.ThrowsAsync<QcValidationException>(() => master.CreateFormAsync(
            new QcFormSaveDto("QC-X", "X", "SAWIT", null, "A", null, null, null, null), "admin"));
        await Assert.ThrowsAsync<QcValidationException>(() => master.CreateFormAsync(
            new QcFormSaveDto("QC-X", "X", "NANAS", null, "Z", null, null, null, null), "admin"));
    }

    [Fact]
    public async Task ApprovalStepRejectsDuplicateLevelAndUnknownGroup()
    {
        await using var db = NewDb();
        var master = new QcMasterService(db);
        var form = await master.CreateFormAsync(new QcFormSaveDto(
            "QC-CONTOH", "Contoh", "NANAS", null, "A", null, null, null, null), "admin");
        await master.CreateStepAsync(new QcApprovalStepSaveDto(form.Id, 1, "Dilaporkan", null, null, null, false), "admin");

        var dup = await Assert.ThrowsAsync<QcValidationException>(() => master.CreateStepAsync(
            new QcApprovalStepSaveDto(form.Id, 1, "Dobel", null, null, null, false), "admin"));
        Assert.Contains("Level 1", dup.Message);

        await Assert.ThrowsAsync<QcValidationException>(() => master.CreateStepAsync(
            new QcApprovalStepSaveDto(form.Id, 2, "P. Jawab Afd", null, 999, null, true), "admin"));

        var step = await master.CreateStepAsync(
            new QcApprovalStepSaveDto(form.Id, 2, "P. Jawab Afd", "ASISTEN_WILAYAH", null, null, true), "admin");
        Assert.True(step.ScopeByAfdeling);
        Assert.Equal("QC_APPROVAL", step.MenuCode);
    }

    [Fact]
    public async Task CreatorStepOnlyAllowedAtLevelOneAndRoundTrips()
    {
        await using var db = NewDb();
        var master = new QcMasterService(db);
        var form = await master.CreateFormAsync(new QcFormSaveDto(
            "QC-CONTOH", "Contoh", "NANAS", null, "A", null, null, null, null), "admin");

        var invalid = await Assert.ThrowsAsync<QcValidationException>(() => master.CreateStepAsync(
            new QcApprovalStepSaveDto(form.Id, 2, "Dilaporkan", null, null, null, false, true), "admin"));
        Assert.Contains("level 1", invalid.Message);

        var creator = await master.CreateStepAsync(
            new QcApprovalStepSaveDto(form.Id, 1, "Dilaporkan", null, null, null, false, true), "admin");
        Assert.True(creator.IsCreatorStep);

        var updated = await master.UpdateStepAsync(creator.Id,
            new QcApprovalStepSaveDto(form.Id, 1, "Dilaporkan", null, null, null, false, false), "admin");
        Assert.False(updated.IsCreatorStep);
    }

    [Fact]
    public async Task PemborongAndGradeRejectDuplicateKode()
    {
        await using var db = NewDb();
        var master = new QcMasterService(db);
        await master.CreatePemborongAsync(new QcPemborongSaveDto("BRU01", "Pemborong Satu", null), "admin");
        await Assert.ThrowsAsync<QcValidationException>(() =>
            master.CreatePemborongAsync(new QcPemborongSaveDto("BRU01", "Dobel", null), "admin"));
        await master.CreateGradeAsync(new QcGradeSaveDto("N1", "N1", "NANAS", 1), "admin");
        await Assert.ThrowsAsync<QcValidationException>(() =>
            master.CreateGradeAsync(new QcGradeSaveDto("N1", "Dobel", null, 2), "admin"));
    }

    [Fact]
    public async Task StandardValidatesRangeOperatorAndUniqueParameter()
    {
        await using var db = NewDb();
        var master = new QcMasterService(db);
        var form = await master.CreateFormAsync(new QcFormSaveDto(
            "QC-CONTOH", "Contoh", "NANAS", null, "A", null, null, null, null), "admin");
        await master.CreateStandardAsync(new QcStandardSaveDto(form.Id, "TOTAL_LOSSES", 5m, "MAX", "Batas losses"), "admin");

        await Assert.ThrowsAsync<QcValidationException>(() =>
            master.CreateStandardAsync(new QcStandardSaveDto(form.Id, "TOTAL_LOSSES", 6m, null, null), "admin"));
        await Assert.ThrowsAsync<QcValidationException>(() =>
            master.CreateStandardAsync(new QcStandardSaveDto(form.Id, "LAIN", 101m, null, null), "admin"));
        await Assert.ThrowsAsync<QcValidationException>(() =>
            master.CreateStandardAsync(new QcStandardSaveDto(form.Id, "LAIN", 5m, "BEBAS", null), "admin"));
    }

    [Fact]
    public async Task MandorRequiresExistingPemborong()
    {
        await using var db = NewDb();
        var master = new QcMasterService(db);
        await Assert.ThrowsAsync<QcValidationException>(() =>
            master.CreateMandorAsync(new QcMandorSaveDto("Mandor Satu", null, "KI-1", 42), "admin"));
        var pemborong = await master.CreatePemborongAsync(new QcPemborongSaveDto("BRU01", "Pemborong Satu", null), "admin");
        var mandor = await master.CreateMandorAsync(new QcMandorSaveDto("Mandor Satu", "KIN1", "KI-1", pemborong.Id), "admin");
        Assert.Equal("Pemborong Satu", mandor.PemborongName);
    }

    [Fact]
    public async Task PersilListFiltersBySumberWilayahAndSearchWithPaging()
    {
        await using var db = NewDb();
        var source = new FakeSource(new()
        {
            ["KLP"] = new() { Raw("1", "A"), Raw("2", "B") },
            ["TNM_NNS"] = new()
            {
                new("TNM_NNS", "9", "TM-19-10AA", null, "NANAS", "RSTM", null, null, null, 1, 2016, 2m, 8, 4800, null, null)
            }
        });
        await new QcPersilSyncService(db, source).SyncAsync(null, "admin");
        var master = new QcMasterService(db);

        var page = await master.ListPersilAsync("KLP", "KIN1", null, null, false, 1, 1);
        Assert.Equal(2, page.Total);
        Assert.Single(page.Rows);
        Assert.Equal(2, page.PerSumber.Count);

        var search = await master.ListPersilAsync(null, null, "NANAS", "TM-19", false, 1, 50);
        Assert.Equal(1, search.Total);
        Assert.Equal("TM-19-10AA", search.Rows[0].PersilKode);
        Assert.NotNull(search.LastSyncedAt);
    }
}