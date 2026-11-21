using AuthUtility.Api.Qc;
using Microsoft.Extensions.Configuration;

namespace AuthUtility.Tests;

// Fase 2: nomor inspeksi atomik per scope form|wilayah|tahun|bulan.
// NpgsqlQcNumberSeqStore (INSERT ... ON CONFLICT ... RETURNING) diuji manual
// terhadap PostgreSQL; di sini logika pola + scope dengan store in-memory.
public class QcNumberingTests
{
    private static IConfiguration Settings() => new ConfigurationBuilder()
        .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: false).Build();

    private static QcNumberGenerator Generator(IConfiguration? config = null) =>
        new(config ?? Settings(), new InMemoryQcNumberSeqStore());

    [Fact]
    public async Task SequentialPerScopeWithDefaultPatternAndPad()
    {
        var numbers = Generator();
        var date = new DateOnly(2026, 11, 19);
        Assert.Equal("QC-PANEN-KLP/W1/2026/11/0001", await numbers.GenerateInspectionNoAsync("QC-PANEN-KLP", "W1", date));
        Assert.Equal("QC-PANEN-KLP/W1/2026/11/0002", await numbers.GenerateInspectionNoAsync("QC-PANEN-KLP", "W1", date));
        Assert.Equal("QC-PANEN-KLP/W1/2026/11/0003", await numbers.GenerateInspectionNoAsync("QC-PANEN-KLP", "  W1  ", date));
    }

    [Fact]
    public async Task ScopesAreIndependentByFormWilayahMonthAndYear()
    {
        var numbers = Generator();
        var nov = new DateOnly(2026, 11, 19);
        var des = new DateOnly(2026, 12, 1);
        var jan = new DateOnly(2027, 1, 5);
        Assert.Equal("QC-PANEN-KLP/W1/2026/11/0001", await numbers.GenerateInspectionNoAsync("QC-PANEN-KLP", "W1", nov));
        Assert.Equal("QC-PANEN-KLP/W2/2026/11/0001", await numbers.GenerateInspectionNoAsync("QC-PANEN-KLP", "W2", nov));
        Assert.Equal("QC-PANEN-KLP/W1/2026/12/0001", await numbers.GenerateInspectionNoAsync("QC-PANEN-KLP", "W1", des));
        Assert.Equal("QC-PANEN-KLP/W1/2027/01/0001", await numbers.GenerateInspectionNoAsync("QC-PANEN-KLP", "W1", jan));
        Assert.Equal("QC-PL-NNS/W1/2026/11/0001", await numbers.GenerateInspectionNoAsync("QC-PL-NNS", "W1", nov));
        Assert.Equal("QC-PANEN-KLP/W1/2026/11/0002", await numbers.GenerateInspectionNoAsync("QC-PANEN-KLP", "W1", nov));
    }

    [Fact]
    public async Task EmptyWilayahBecomesDashScope()
    {
        var numbers = Generator();
        var date = new DateOnly(2026, 11, 19);
        Assert.Equal("QC-PANEN-KLP/-/2026/11/0001", await numbers.GenerateInspectionNoAsync("QC-PANEN-KLP", null, date));
        Assert.Equal("QC-PANEN-KLP/-/2026/11/0002", await numbers.GenerateInspectionNoAsync("QC-PANEN-KLP", "   ", date));
    }

    [Fact]
    public async Task PatternAndPadAreConfigurable()
    {
        var config = new ConfigurationBuilder().AddConfiguration(Settings())
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Qc:Numbering:InspectionPattern"] = "ICAR/{wilayah}/{yy}-{seq}",
                ["Qc:Numbering:InspectionSeqPad"] = "6"
            }).Build();
        var numbers = Generator(config);
        // Token tak dikenal ({yy}) dibiarkan apa adanya; token dikenal diganti.
        var no = await numbers.GenerateInspectionNoAsync("QC-PL-NNS", "W3", new DateOnly(2026, 11, 19));
        Assert.Equal("ICAR/W3/{yy}-000001", no);
    }

    [Fact]
    public void FindingNumberStillIdentityBasedWithCommodityPrefix()
    {
        var numbers = Generator();
        var year = DateTime.UtcNow.AddHours(7).Year;
        Assert.Equal($"QC-KLP-F-{year}-000042", numbers.GenerateFindingNo("KELAPA", 42));
        Assert.Equal($"QC-NNS-F-{year}-000007", numbers.GenerateFindingNo("NANAS", 7));
    }

    [Fact]
    public async Task InMemoryStoreIsConcurrencySafe()
    {
        var store = new InMemoryQcNumberSeqStore();
        var results = await Task.WhenAll(Enumerable.Range(0, 200).Select(_ => store.NextAsync("scope")));
        Assert.Equal(Enumerable.Range(1, 200), results.OrderBy(x => x));
    }
}