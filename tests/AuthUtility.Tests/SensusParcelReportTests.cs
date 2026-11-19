using AuthUtility.Api.Sensus;
using AuthUtility.Domain.Entities;
using AuthUtility.Application.DTOs.Sensus;
using AuthUtility.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AuthUtility.Tests;

public class SensusParcelReportTests
{
    private static SnsHeader Parcel(string status = SnsStatus.Approved)
    {
        SnsJenis Kind(int id, string name, decimal threshold) => new()
        {
            Id = id, Code = name, Name = name, ReportGroup = id == 2 ? "PENYAKIT" : "HAMA",
            Rules = new List<SnsHptRule> { new() { Id = id, JenisId = id, Active = true,
                ParameterCode = "PERSEN_TERSERANG", Operator = ">=", ThresholdValue = threshold,
                Message = name + " rekomendasi master", ElseMessage = name + " monitoring master" } }
        };
        return new SnsHeader
        {
            Id = 10, Status = status, PersilSumber = "KLP", PersilId = "P001", PersilKode = "P001",
            TanggalSensus = new(2026, 11, 18), JumlahPokokSampel = 100,
            JumlahJalur = 5, LuasHa = 12, JumlahPokokPersil = 1000, PohonMatiPetir = 1,
            HptDetails = new List<SnsHptDetail>
            {
                new() { HptId = 1, Hpt = Kind(1, "Jenis baru admin", 5), JumlahSerangan = 6 },
                new() { HptId = 2, Hpt = Kind(2, "Penyakit baru admin", 3), JumlahSerangan = 2 }
            }
        };
    }

    private static SensusBusinessFilter Filter() => new() { DateFrom = new(2026, 11, 1), DateTo = new(2026, 11, 30) };

    [Fact]
    public void MultipleHptUseTheirOwnCountsAndRulesWithOneSharedParcelContext()
    {
        var report = SensusBusinessReportQuery.Build(new[] { Parcel(), Parcel(SnsStatus.Submitted) }, new(), Filter());
        Assert.Equal(1, report.ApprovedBeforeDedup);
        Assert.Equal(2, report.Observations.Count);
        Assert.Equal(new decimal?[] { 6m, 2m }, report.Observations.Select(o => o.Persen));
        Assert.True(report.Observations[0].Evaluation.PerluPengendalian);
        Assert.False(report.Observations[1].Evaluation.PerluPengendalian);
        var matrix = Assert.Single(report.Rekap);
        Assert.Equal(100, matrix.Sampel);
        Assert.Equal(12m, matrix.LuasHa);
        Assert.Equal(1, matrix.MatiPetir);
        Assert.Equal(5, matrix.Jalur);
    }

    [Fact]
    public void HptFilterIncludesCombinedHeaderAndReturnsOnlyRequestedObservation()
    {
        var filter = Filter(); filter.JenisId = 2;
        var report = SensusBusinessReportQuery.Build(new[] { Parcel() }, new(), filter);
        var observation = Assert.Single(report.Observations);
        Assert.Equal(2, observation.JenisId);
        Assert.Equal(2, observation.Terserang);
    }

    [Fact]
    public void HeaderEvaluationUsesMostSevereChildWithoutAddingOverlappingCounts()
    {
        var h = Parcel();
        var result = SensusHptEvaluation.Evaluate(h);
        Assert.True(result.PerluPengendalian);
        Assert.Contains("Jenis baru admin", result.Message);
        Assert.Equal(6m, result.Nilai);
        h.HptDetails.First().JumlahSerangan = 0;
        Assert.False(SensusHptEvaluation.Evaluate(h).PerluPengendalian);
    }

    [Fact]
    public void RechecksDeduplicatePerHptAndZeroLegacyDenominatorRemainsUnknown()
    {
        var older = Parcel(); older.Id = 9; older.TanggalSensus = new(2026, 11, 17);
        var latest = Parcel(); latest.HptDetails.First().JumlahSerangan = 9;
        var legacy = new SnsHeader { Id = 11, JenisId = 3, PersilId = "OTHER", Status = SnsStatus.Approved,
            TanggalSensus = new(2026, 11, 18), JumlahPokokSampel = 0 };
        var report = SensusBusinessReportQuery.Build(new[] { older, latest, legacy }, new(), Filter());
        Assert.Equal(3, report.Observations.Count);
        Assert.Equal(9m, report.Observations.Single(o => o.JenisId == 1).Persen);
        Assert.Null(report.Observations.Single(o => o.JenisId == 3).Persen);
    }

    [Fact]
    public async Task DatabaseReportAndEvaluationReadChildrenAndRetainInactiveHistoricalHpt()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var h = Parcel(); h.HptDetails.Last().Hpt!.Active = false;
        h.PetugasUserId = 1;
        db.Users.Add(new UtlUser { Id = 1, UserId = "petugas", Name = "Petugas", GroupId = 1, PasswordHash = "test" });
        db.SnsHeaders.Add(h); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var filter = Filter(); filter.JenisId = 2;
        var report = await SensusBusinessReportQuery.ReadAsync(db, filter);
        Assert.NotNull(report);
        Assert.Equal(2, Assert.Single(report!.Observations).JenisId);
        Assert.Contains(report.Jenis, k => k.Id == 2);
        var results = await SensusHptEvaluation.EvaluateAsync(db, new long[] { h.Id }, 2);
        Assert.False(results[h.Id].PerluPengendalian);
        Assert.True((await SensusHptEvaluation.EvaluateAsync(db, new long[] { h.Id }))[h.Id].PerluPengendalian);
    }

    [Fact]
    public void DashboardRanksEachChildWithoutMultiplyingHeaderTotals()
    {
        var h = Parcel();
        var monitoring = new SnsMonitoringDto(1, 0, 1, 0, 0, new(), new(), new(), new(), "KLP", new(), null);
        var dashboard = SensusDashboardCalculator.Build(monitoring, new[] { h },
            new Dictionary<long, (string Tingkat, decimal? Rata)> { [h.Id] = ("BERAT", null) });
        Assert.Equal(1, dashboard.Total);
        Assert.Equal(1, dashboard.PerTingkat.Values.Sum());
        Assert.Equal(1, Assert.Single(dashboard.TrenBulanan).Total);
        Assert.Equal(2, dashboard.TopPersil.Count);
        Assert.Equal(6m, dashboard.TopPersil.Single(o => o.JenisId == 1).PersenTerserang);
        Assert.Equal(2m, dashboard.TopPersil.Single(o => o.JenisId == 2).PersenTerserang);
    }

    [Fact]
    public void ReportUsesCurrentMasterWhileSavedSnapshotRemainsAuditEvidence()
    {
        var h = Parcel(); var child = h.HptDetails.First();
        child.HasilEvaluasi = JsonSerializer.Serialize(SensusHptEvaluation.Evaluate(SensusParcelHpt.ForHpt(h, child)));
        child.Hpt!.Rules.Single().ThresholdValue = 50;
        var report = SensusBusinessReportQuery.Build(new[] { h }, new(), Filter());
        Assert.False(report.Observations.Single(o => o.JenisId == 1).Evaluation.PerluPengendalian);
        Assert.True(SensusParcelHpt.Dtos(h).Single(o => o.HptId == 1).HasilEvaluasi.PerluPengendalian);
    }
}
