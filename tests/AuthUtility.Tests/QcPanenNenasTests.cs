using System.Security.Claims;
using System.Text.Json;
using AuthUtility.Api.Controllers;
using AuthUtility.Api.Qc;
using AuthUtility.Application.DTOs.Qc;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;

namespace AuthUtility.Tests;

// Fase 3: detail Panen Nenas disimpan di tabel typed TrnQcInspSampelPanenNenas,
// bukan DataJson. DataJson hanya menyimpan ekstra header (mis. umurPanen).
// Total/% tidak disimpan — dihitung view PostgreSQL (diuji manual saat migrasi).
public class QcPanenNenasTests
{
    private static IConfiguration Settings() => new ConfigurationBuilder()
        .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: false).Build();

    private static JsonDocument Data(string json) => JsonDocument.Parse(json);

    private const string TwoRows = """
        { "umurPanen": "14 bulan", "samples": [
          { "nomorJalur": "J-01", "jumlahPohon": 100, "buahTidakDipetik": 4, "buahTelahDipetik": 90, "buahTerpotong": 2, "keterangan": "Aman" },
          { "nomorJalur": "J-02", "jumlahPohon": 120, "buahTidakDipetik": 6, "buahTelahDipetik": 100, "buahTerpotong": 3, "keterangan": null }
        ] }
        """;

    [Fact]
    public async Task Create_PersistsTypedRows_AndStripsSamplesFromDataJson()
    {
        await using var db = await Database();
        var controller = Controller(db);

        var result = await controller.CreateInspection(new CreateQcInspectionRequest
        {
            FormCode = "QC-PANEN-NNS",
            WilayahKode = "2",
            Data = Data(TwoRows),
        }, default);

        Assert.IsType<OkObjectResult>(result);
        var rows = db.QcPanenNenasSamples.OrderBy(x => x.RowNo).ToList();
        Assert.Equal(2, rows.Count);
        Assert.Equal(1, rows[0].RowNo);
        Assert.Equal("J-01", rows[0].NomorJalur);
        Assert.Equal(100, rows[0].JumlahPohon);
        Assert.Equal(4, rows[0].BuahTidakDipetik);
        Assert.Equal(90, rows[0].BuahTelahDipetik);
        Assert.Equal(2, rows[0].BuahTerpotong);
        Assert.Equal("Aman", rows[0].Keterangan);
        Assert.Null(rows[1].Keterangan);
        Assert.All(rows, r => Assert.Equal(db.QcInspections.Single().Id, r.InspectionId));

        var inspection = db.QcInspections.Single();
        Assert.DoesNotContain("samples", inspection.DataJson);
        Assert.Contains("umurPanen", inspection.DataJson);
    }

    [Fact]
    public async Task Detail_ReturnsSamplesFromTypedTable_WithHeaderExtras()
    {
        await using var db = await Database();
        var controller = Controller(db);
        await controller.CreateInspection(new CreateQcInspectionRequest { FormCode = "QC-PANEN-NNS", Data = Data(TwoRows) }, default);
        var id = db.QcInspections.Single().Id;

        var result = await controller.GetInspection(id, default);
        var dto = Assert.IsType<QcInspectionDetailDto>(Assert.IsType<OkObjectResult>(result).Value);
        var samples = dto.Data!.RootElement.GetProperty("samples");
        Assert.Equal(2, samples.GetArrayLength());
        Assert.Equal("J-02", samples[1].GetProperty("nomorJalur").GetString());
        Assert.Equal(100, samples[1].GetProperty("buahTelahDipetik").GetInt32());
        Assert.Equal("14 bulan", dto.Data.RootElement.GetProperty("umurPanen").GetString());
    }

    [Fact]
    public async Task Update_ReplacesTypedRows_AndKeepsHeaderExtras()
    {
        await using var db = await Database();
        var controller = Controller(db);
        await controller.CreateInspection(new CreateQcInspectionRequest { FormCode = "QC-PANEN-NNS", Data = Data(TwoRows) }, default);
        var id = db.QcInspections.Single().Id;

        var result = await controller.UpdateInspection(id, new UpdateQcInspectionRequest
        {
            Data = Data("""{ "umurPanen": "15 bulan", "samples": [ { "nomorJalur": "J-09", "jumlahPohon": 80, "buahTidakDipetik": 1, "buahTelahDipetik": 70, "buahTerpotong": 0 } ] }"""),
        }, default);

        Assert.IsType<OkObjectResult>(result);
        var row = Assert.Single(db.QcPanenNenasSamples.ToList());
        Assert.Equal(1, row.RowNo);
        Assert.Equal("J-09", row.NomorJalur);
        Assert.Equal(80, row.JumlahPohon);
        Assert.Equal(0, row.BuahTerpotong);
        Assert.Contains("15 bulan", db.QcInspections.Single().DataJson);
    }

    [Fact]
    public async Task Update_InvalidSamples_DoesNotMutateExistingRows()
    {
        await using var db = await Database();
        var controller = Controller(db);
        await controller.CreateInspection(new CreateQcInspectionRequest { FormCode = "QC-PANEN-NNS", Data = Data(TwoRows) }, default);
        var id = db.QcInspections.Single().Id;

        var result = await controller.UpdateInspection(id, new UpdateQcInspectionRequest
        {
            Data = Data("""{ "samples": [ { "nomorJalur": "J-01", "jumlahPohon": -5, "buahTidakDipetik": 0, "buahTelahDipetik": 0, "buahTerpotong": 0 } ] }"""),
        }, default);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(2, db.QcPanenNenasSamples.Count());
        Assert.Contains("umurPanen", db.QcInspections.Single().DataJson);
    }

    [Theory]
    [InlineData("""{ "samples": [ { "jumlahPohon": 1, "buahTidakDipetik": 0, "buahTelahDipetik": 0, "buahTerpotong": 0 } ] }""")] // tanpa nomor jalur
    [InlineData("""{ "samples": [ { "nomorJalur": "J-01", "jumlahPohon": 1, "buahTidakDipetik": -1, "buahTelahDipetik": 0, "buahTerpotong": 0 } ] }""")] // negatif
    [InlineData("""{ "samples": [ { "nomorJalur": "J-01", "jumlahPohon": 1.5, "buahTidakDipetik": 0, "buahTelahDipetik": 0, "buahTerpotong": 0 } ] }""")] // pecahan
    [InlineData("""{ "samples": [ { "nomorJalur": "J-01", "jumlahPohon": 1, "buahTidakDipetik": 0, "buahTelahDipetik": 0 } ] }""")] // kurang kolom
    [InlineData("""{ "samples": "bukan-array" }""")] // format salah
    public async Task Create_InvalidSamples_RejectedWithoutSaving(string json)
    {
        await using var db = await Database();
        var result = await Controller(db).CreateInspection(new CreateQcInspectionRequest
        {
            FormCode = "QC-PANEN-NNS",
            Data = Data(json),
        }, default);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(db.QcInspections.ToList());
        Assert.Empty(db.QcPanenNenasSamples.ToList());
    }

    [Fact]
    public async Task Create_RejectsMoreThanTwentyRows()
    {
        await using var db = await Database();
        var rows = string.Join(",", Enumerable.Range(1, 21).Select(i =>
            $$"""{ "nomorJalur": "J-{{i}}", "jumlahPohon": 1, "buahTidakDipetik": 0, "buahTelahDipetik": 0, "buahTerpotong": 0 }"""));
        var result = await Controller(db).CreateInspection(new CreateQcInspectionRequest
        {
            FormCode = "QC-PANEN-NNS",
            Data = Data($$"""{ "samples": [ {{rows}} ] }"""),
        }, default);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("20", bad.Value!.ToString());
    }

    [Fact]
    public void TryParse_AcceptsStringNumbers_AndEmptyPayload()
    {
        Assert.True(QcPanenNenasDetail.TryParse(null, out var empty, out var noError));
        Assert.Empty(empty);
        Assert.Null(noError);

        var json = JsonDocument.Parse("""{ "samples": [ { "nomorJalur": 5, "jumlahPohon": "100", "buahTidakDipetik": "2", "buahTelahDipetik": "90", "buahTerpotong": "1" } ] }""");
        Assert.True(QcPanenNenasDetail.TryParse(json.RootElement, out var rows, out var error));
        Assert.Null(error);
        var row = Assert.Single(rows);
        Assert.Equal("5", row.NomorJalur);
        Assert.Equal(100, row.JumlahPohon);
        Assert.Equal(2, row.BuahTidakDipetik);
    }

    [Fact]
    public void StripSamples_KeepsOtherHeaderFields()
    {
        var stripped = QcPanenNenasDetail.StripSamples(TwoRows);
        Assert.DoesNotContain("samples", stripped);
        using var doc = JsonDocument.Parse(stripped!);
        Assert.Equal("14 bulan", doc.RootElement.GetProperty("umurPanen").GetString());
        Assert.Null(QcPanenNenasDetail.StripSamples(null));
    }

    private static ControllerContext Context() => new() { HttpContext = new DefaultHttpContext
    {
        User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "1") }, "test"))
    } };

    private static QcInspectionsController Controller(AppDbContext db) =>
        new(db, new QcStatusEngine(), new QcNumberGenerator(Settings(), new InMemoryQcNumberSeqStore()), new QcApprovalFlow(db))
        { ControllerContext = Context() };

    private static async Task<AppDbContext> Database()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options;
        var db = new AppDbContext(options);
        var group = new UtlGroup { Id = 1, Code = "QC_TEAM", Name = "Test QC", Active = true };
        db.Groups.Add(group);
        db.Menus.AddRange(
            new UtlMenu { Id = 1, Code = "QC_BUAT_INSPEKSI", Name = "QC Buat Inspeksi", Active = true },
            new UtlMenu { Id = 2, Code = "QC_APPROVAL", Name = "QC Approval", Active = true });
        db.Users.Add(new UtlUser { Id = 1, UserId = "reporter", Name = "Test Reporter", PasswordHash = "test", GroupId = 1, Group = group, Active = true });
        db.GroupMenus.AddRange(
            new UtlGroupMenu { GroupId = 1, MenuId = 1, CanCreate = true, CanUpdate = true, CanRead = true, CanView = true },
            new UtlGroupMenu { GroupId = 1, MenuId = 2, CanApprove = true });
        db.QcForms.Add(new QcForm { Id = 1, Code = "QC-PANEN-NNS", Name = "Panen Nenas", Commodity = "NANAS", Active = true });
        await db.SaveChangesAsync();
        return db;
    }
}