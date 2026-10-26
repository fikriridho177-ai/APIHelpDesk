using AuthUtility.Api.Sensus;
using AuthUtility.Domain.Entities;
using Xunit;

namespace AuthUtility.Tests;

public class RnaRingkasanTests
{
    private static readonly DateOnly D1 = new(2026, 10, 1);
    private static readonly DateOnly D2 = new(2026, 10, 15);

    private static RnaJenisInfo UlatApi(bool indikator = true) =>
        new(2, "ULAT_API", "Sensus Ulat Api", "HPT", "KELAPA", null, 5m, 10m, indikator);

    [Fact]
    public void Tingkat_persil_memakai_sensus_disetujui_terakhir()
    {
        var headers = new List<RnaHeaderRow>
        {
            new(1, 2, SnsStatus.Approved, D1, "KLP", "A1"),   // lama: berat
            new(2, 2, SnsStatus.Approved, D2, "KLP", "A1"),   // terbaru: ringan -> dipakai
            new(3, 2, SnsStatus.Approved, D2, "KLP", "B7"),   // sedang
            new(4, 2, SnsStatus.Submitted, D2, "KLP", "C3"),  // belum disetujui: tidak dinilai
        };
        var nilai = new Dictionary<long, decimal?> { [1] = 12m, [2] = 2m, [3] = 6m };

        var r = RnaRingkasanCalculator.Build([UlatApi()], headers, nilai).Single();

        Assert.Equal(4, r.SensusPeriode);
        Assert.Equal(1, r.MenungguApproval);
        Assert.Equal(3, r.Disetujui);
        Assert.Equal(2, r.PersilDinilai);
        Assert.Equal(0, r.Berat);
        Assert.Equal(1, r.Sedang);
        Assert.Equal(1, r.Ringan);
        Assert.Equal(D2, r.SensusTerakhir);
    }

    [Fact]
    public void Ambang_berat_inklusif_dan_nol_berarti_nihil()
    {
        var headers = new List<RnaHeaderRow>
        {
            new(1, 2, SnsStatus.Approved, D1, "KLP", "A1"),
            new(2, 2, SnsStatus.Approved, D1, "KLP", "A2"),
        };
        var nilai = new Dictionary<long, decimal?> { [1] = 10m, [2] = 0m };

        var r = RnaRingkasanCalculator.Build([UlatApi()], headers, nilai).Single();

        Assert.Equal(1, r.Berat);
        Assert.Equal(1, r.Nihil);
    }

    [Fact]
    public void Jenis_tanpa_indikator_atau_tanpa_data_tetap_tampil_dengan_nol()
    {
        var kelapa = new RnaJenisInfo(6, "KELAPA", "Sensus Kelapa", "TANAMAN", "KELAPA", null, null, null, false);
        var headers = new List<RnaHeaderRow> { new(1, 2, SnsStatus.Approved, D1, "KLP", "A1") };

        var result = RnaRingkasanCalculator.Build([UlatApi(indikator: false), kelapa], headers, new Dictionary<long, decimal?>());

        Assert.Equal(1, result[0].PersilDinilai);
        Assert.Equal(0, result[0].Berat + result[0].Sedang + result[0].Ringan + result[0].Nihil);
        Assert.Equal(0, result[1].SensusPeriode);
        Assert.Null(result[1].SensusTerakhir);
    }
}
