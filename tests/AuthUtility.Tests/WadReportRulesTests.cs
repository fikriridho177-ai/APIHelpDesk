using AuthUtility.Api.Wad;
using AuthUtility.Domain.Entities;

namespace AuthUtility.Tests;

// Aturan bisnis pelaporan WAD: ambang subsiden, durasi genangan, progres projek,
// validasi input per modul, dan kebijakan berkas foto.
public class WadReportRulesTests
{
    private static readonly DateOnly Today = new(2026, 11, 16);

    private static WadObject PeatStake(decimal? referenceCm = 1000.000m) => new()
    {
        Id = 1,
        Module = WadReportModule.Peat,
        Code = "SG-01",
        Name = "Patok SG-01",
        ObjectType = "PATOK",
        ReferenceValueCm = referenceCm
    };

    private static WadObject FloodParcel(decimal areaHa = 4.5m) => new()
    {
        Id = 2,
        Module = WadReportModule.Flood,
        Code = "P-101",
        Name = "Persil P-101",
        ObjectType = "PERSIL",
        AreaHa = areaHa
    };

    private static WadObject Project(decimal? targetQty, string? unit = "m3") => new()
    {
        Id = 3,
        Module = WadReportModule.Project,
        Code = "PRJ-01",
        Name = "Normalisasi Kanal",
        ObjectType = "PROYEK",
        TargetQty = targetQty,
        Unit = unit
    };

    /* ============================ SUBSIDEN ============================ */

    [Theory]
    [InlineData(null, null)]
    [InlineData(-1.2, "NORMAL")]          // permukaan naik, tetap normal
    [InlineData(0d, "NORMAL")]
    [InlineData(1.999, "NORMAL")]
    [InlineData(2d, "PERLU_VERIFIKASI")]
    [InlineData(3.5, "PERLU_VERIFIKASI")]
    [InlineData(4d, "KRITIS")]
    [InlineData(9.1, "KRITIS")]
    public void PeatCondition_MemakaiAmbangMaster(double? dropCm, string? expected)
    {
        Assert.Equal(expected, WadReportRules.PeatCondition((decimal?)dropCm, 2m, 4m));
    }

    [Fact]
    public void ValidatePeat_PatokTanpaAcuan_TetapMenyimpanPengukuran()
    {
        var input = new WadReportInput
        {
            ObjectId = 1,
            ObservedDate = Today,
            MeasurementValue = 995m,
            MeasurementUnit = "CM"
        };

        var error = WadReportRules.ValidatePeat(input, PeatStake(referenceCm: null));

        Assert.Null(error);
        Assert.Null(WadReportRules.PeatDrop(null, null, 995m));
        Assert.Null(WadReportRules.PeatCondition(5m));
    }

    [Fact]
    public void ValidatePeat_SatuanMeter_Diterima()
    {
        var input = new WadReportInput
        {
            ObjectId = 1,
            ObservedDate = Today,
            MeasurementValue = 9.95m,
            MeasurementUnit = "m"
        };

        Assert.Null(WadReportRules.ValidatePeat(input, PeatStake()));
    }

    [Fact]
    public void ValidatePeat_SatuanTidakDikenal_Ditolak()
    {
        var input = new WadReportInput
        {
            ObjectId = 1,
            ObservedDate = Today,
            MeasurementValue = 995m,
            MeasurementUnit = "MM"
        };

        Assert.Contains("CM atau M", WadReportRules.ValidatePeat(input, PeatStake()));
    }

    /* ============================ GENANGAN ============================ */

    [Fact]
    public void FloodDuration_TanpaTanggalMulai_Null()
    {
        Assert.Null(WadReportRules.FloodDurationDays(Today, null));
    }

    [Fact]
    public void FloodDuration_DihitungDariTanggalMulaiSampaiPengamatan()
    {
        Assert.Equal(10, WadReportRules.FloodDurationDays(Today, new DateOnly(2026, 11, 6)));
    }

    [Fact]
    public void FloodDuration_TanggalMulaiSetelahPengamatan_Null()
    {
        Assert.Null(WadReportRules.FloodDurationDays(Today, new DateOnly(2026, 11, 20)));
    }

    [Fact]
    public void ValidateFlood_LuasMelebihiLuasPersil_Ditolak()
    {
        var input = new WadReportInput
        {
            ObjectId = 2,
            ObservedDate = Today,
            Condition = "MELUAS",
            FloodAreaHa = 5.25m
        };

        var error = WadReportRules.ValidateFlood(input, FloodParcel(areaHa: 4.5m));

        Assert.Contains("luas persil", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateFlood_TanpaTanggalMulai_TetapDiterima()
    {
        var input = new WadReportInput
        {
            ObjectId = 2,
            ObservedDate = Today,
            Condition = "SURUT",
            FloodAreaHa = 1.25m
        };

        Assert.Null(WadReportRules.ValidateFlood(input, FloodParcel()));
    }

    /* ============================ PROJEK ============================ */

    [Theory]
    [InlineData(2400d, 600d, null, 25)]
    [InlineData(2400d, 1200d, null, 50)]
    [InlineData(2400d, 3000d, null, 100)]   // realisasi melebihi target dibatasi 100
    [InlineData(2400d, -1d, null, 0)]       // nilai negatif dibatasi 0
    [InlineData(1000d, 333d, null, 33)]     // pembulatan away-from-zero
    [InlineData(1000d, 335d, null, 34)]
    [InlineData(null, null, 40, 40)]      // tanpa target: persen manual
    [InlineData(null, null, 140, 100)]
    [InlineData(null, null, null, null)]
    public void ProjectPercent_MengikutiTargetKuantitasAtauPersenManual(
        double? target, double? realization, int? manual, int? expected)
    {
        var result = WadReportRules.ProjectPercent(
            (decimal?)target, (decimal?)realization, manual);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(100, "2026-12-01", "SELESAI")]
    [InlineData(80, "2026-12-01", "BERJALAN")]
    [InlineData(80, "2026-11-01", "TERLAMBAT")]
    [InlineData(80, null, "BERJALAN")]
    public void ProjectStatus_MemakaiTanggalTarget(int percent, string? target, string expected)
    {
        var targetDate = target == null ? (DateOnly?)null : DateOnly.Parse(target);

        Assert.Equal(expected, WadReportRules.ProjectStatus(percent, targetDate, Today));
    }

    [Fact]
    public void ValidateProject_Bertarget_TanpaRealisasi_Ditolak()
    {
        var input = new WadReportInput
        {
            ObjectId = 3,
            ObservedDate = Today,
            WorkDescription = "Penghitungan galian kanal",
            ProgressPercent = 40
        };

        Assert.Contains("Realisasi", WadReportRules.ValidateProject(input, Project(2400m)));
    }

    [Fact]
    public void ValidateProject_TanpaTarget_TanpaPersen_Ditolak()
    {
        var input = new WadReportInput
        {
            ObjectId = 3,
            ObservedDate = Today,
            WorkDescription = "Pemeliharaan sifon"
        };

        Assert.Contains("Persentase", WadReportRules.ValidateProject(input, Project(null)));
    }

    [Fact]
    public void ValidateProject_UraianTerlaluPendek_Ditolak()
    {
        var input = new WadReportInput
        {
            ObjectId = 3,
            ObservedDate = Today,
            WorkDescription = "cuci",
            ProgressPercent = 10
        };

        Assert.Contains("Uraian", WadReportRules.ValidateProject(input, Project(null)));
    }

    /* ============================ PENGAMATAN AIR ============================ */

    [Theory]
    [InlineData("BAIK", null, true)]
    [InlineData("PERLU_PERBAIKAN", 125.5, true)]
    [InlineData("RUSAK", 0d, true)]
    [InlineData("NORMAL", null, false)]
    [InlineData(null, null, false)]
    public void ValidateWater_MenerimaHanyaKondisiBangunan(string? condition, double? level, bool valid)
    {
        var input = new WadReportInput
        {
            ObjectId = 1,
            ObservedDate = Today,
            Condition = condition,
            WaterLevelCm = (decimal?)level
        };

        Assert.Equal(valid, WadReportRules.ValidateWater(input) == null);
    }

    [Fact]
    public void ValidateCommon_TanggalDefault_Ditolak()
    {
        var input = new WadReportInput
        {
            ObjectId = 1,
            Condition = "BAIK"
        };

        Assert.Contains("Tanggal", WadReportRules.ValidateWater(input));
    }

    [Theory]
    [InlineData("ELEVATION", 100, 97, 3)]
    [InlineData("EXPOSED_ROD", 0, 3, 3)]
    [InlineData("EXPOSED_ROD", 10, 8, -2)]
    public void PeatDrop_MengikutiKonvensiMaster(string method, int reference, int measured, int expected)
    {
        Assert.Equal(expected, WadReportRules.PeatDrop(method, reference, measured));
    }

    [Fact]
    public void PeatDrop_TanpaMetodeAtauAcuan_TidakMengarangNilai()
    {
        Assert.Null(WadReportRules.PeatDrop("UNKNOWN", 100m, 99m));
        Assert.Null(WadReportRules.PeatDrop("ELEVATION", null, 99m));
        Assert.Null(WadReportRules.PeatCondition(3m, 5m, 2m));
    }

    [Theory]
    [InlineData("SURUT", true)]
    [InlineData("MELUAS", false)]
    public void Flood_LuasNol_HanyaSurut(string condition, bool valid)
    {
        Assert.Equal(valid, WadReportRules.ValidateFlood(new WadReportInput { ObservedDate = Today, Condition = condition, FloodAreaHa = 0 }, FloodParcel()) == null);
    }

    [Fact]
    public void Peat_NilaiEkstrem_DitolakTanpaOverflow()
    {
        foreach (var unit in new[] { "CM", "M" })
        foreach (var value in new[] { decimal.MinValue, decimal.MaxValue })
            Assert.NotNull(WadReportRules.ValidatePeat(new WadReportInput
            {
                ObservedDate = Today, MeasurementUnit = unit, MeasurementValue = value
            }, PeatStake()));
    }

    [Fact]
    public void Peat_BatasSatuan_Diterima()
    {
        foreach (var unit in new[] { "CM", "M" })
        foreach (var sign in new[] { -1, 1 })
            Assert.Null(WadReportRules.ValidatePeat(new WadReportInput
            {
                ObservedDate = Today, MeasurementUnit = unit,
                MeasurementValue = sign * (unit == "M" ? 2000m : 200000m)
            }, PeatStake()));
    }

    [Fact]
    public void Flood_KapasitasPenyimpanan_DivalidasiTanpaLuasMaster()
    {
        var input = new WadReportInput { ObservedDate = Today, Condition = "MELUAS", FloodAreaHa = 99999999.9999m };
        Assert.Null(WadReportRules.ValidateFlood(input, FloodParcel(0m)));
        input.FloodAreaHa = 100000000m;
        Assert.NotNull(WadReportRules.ValidateFlood(input, FloodParcel(0m)));
    }

    [Fact]
    public void Project_KapasitasPenyimpanan_DivalidasiUntukSemuaJenisTarget()
    {
        foreach (var target in new decimal?[] { null, 100m })
        {
            var input = new WadReportInput
            {
                ObservedDate = Today, WorkDescription = "Pekerjaan pengujian",
                RealizationQty = 99999999999.999m, ProgressPercent = 50
            };
            Assert.Null(WadReportRules.ValidateProject(input, Project(target)));
            input.RealizationQty = 100000000000m;
            Assert.NotNull(WadReportRules.ValidateProject(input, Project(target)));
        }
    }

    [Fact]
    public void ProjectPercent_NilaiEkstrem_DibatasiTanpaOverflow()
    {
        Assert.Equal(100, WadReportRules.ProjectPercent(0.001m, decimal.MaxValue, null));
        Assert.Equal(0, WadReportRules.ProjectPercent(0.001m, decimal.MinValue, null));
    }

    [Fact]
    public void Foto_RusakMeskipunSignatureJpeg_Ditolak()
    {
        using var stream = new MemoryStream(new byte[] { 255, 216, 255, 224, 0, 16 });
        Assert.NotNull(WadPhotoPolicy.ValidateImage(stream));
    }

    [Theory]
    [InlineData(SkiaSharp.SKEncodedImageFormat.Jpeg)]
    [InlineData(SkiaSharp.SKEncodedImageFormat.Png)]
    [InlineData(SkiaSharp.SKEncodedImageFormat.Webp)]
    public void Foto_GambarLengkap_Diterima(SkiaSharp.SKEncodedImageFormat format)
    {
        using var bitmap = new SkiaSharp.SKBitmap(8, 8);
        bitmap.Erase(SkiaSharp.SKColors.Teal);
        using var data = bitmap.Encode(format, 90);
        using var stream = data.AsStream();
        Assert.Null(WadPhotoPolicy.ValidateImage(stream));
    }

    /* ============================ FOTO ============================ */

    [Fact]
    public void FotoPolicy_JpegValid_Diterima()
    {
        byte[] jpeg = { 255, 216, 255, 224, 0, 16 };

        Assert.Null(WadPhotoPolicy.Validate("foto.jpg", "image/jpeg", jpeg.Length, jpeg));
    }

    [Fact]
    public void FotoPolicy_IsiTidakSesuaiEkstensi_Ditolak()
    {
        byte[] png = { 137, 80, 78, 71, 13, 10, 26, 10 };

        Assert.Contains("JPG", WadPhotoPolicy.Validate("foto.jpg", "image/jpeg", png.Length, png));
    }

    [Fact]
    public void FotoPolicy_MimeTidakSesuai_Ditolak()
    {
        byte[] jpeg = { 255, 216, 255, 224, 0, 16 };

        Assert.NotNull(WadPhotoPolicy.Validate("foto.jpg", "image/png", jpeg.Length, jpeg));
    }

    [Fact]
    public void FotoPolicy_EkstensiTidakDidukung_Ditolak()
    {
        byte[] gif = "GIF89a"u8.ToArray();

        Assert.NotNull(WadPhotoPolicy.Validate("foto.gif", "image/gif", gif.Length, gif));
    }

    [Fact]
    public void FotoPolicy_UkuranMelebihiBatas_Ditolak()
    {
        byte[] jpeg = { 255, 216, 255, 224 };

        var error = WadPhotoPolicy.Validate("foto.jpg", "image/jpeg", WadReportRules.MaxPhotoBytes + 1, jpeg);

        Assert.Contains("5 MB", error);
    }

    [Fact]
    public void FotoPolicy_WebpValid_Diterima()
    {
        var webp = new byte[16];
        "RIFF"u8.CopyTo(webp);
        "WEBP"u8.CopyTo(webp.AsSpan(8));

        Assert.Null(WadPhotoPolicy.Validate("foto.webp", "image/webp", webp.Length, webp));
    }

    [Fact]
    public void FotoPolicy_PathRelatif_TidakBolehKeluarDirektori()
    {
        var root = Path.Combine(Path.GetTempPath(), "wad-tests");

        Assert.Null(WadPhotoPolicy.ResolvePath(root, "../rahasia.jpg"));
        Assert.Null(WadPhotoPolicy.ResolvePath(root, "C:\\Windows\\system32\\x.jpg"));
        Assert.NotNull(WadPhotoPolicy.ResolvePath(root, "water/2026/11/1/foto.jpg"));
    }
}
