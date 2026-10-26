using AuthUtility.Domain.Entities;

namespace AuthUtility.Api.Layanan;

// Satu tiket beserta nama divisi & jenis permintaannya.
public record ReportRow(LynRequest Request, string Division, string Jenis);

// Angka satu kelompok (total, per divisi, per jenis, per prioritas).
public record ReportGroup(
    string Division,
    string Label,
    int Total,
    int MenungguApproval,
    int Disetujui,
    int DalamProses,
    int Selesai,
    int DitolakBatal,
    int Terlambat,
    double? RataHariSelesai,
    double? PersenTepatWaktu);

public record ReportMonth(string Month, int Masuk, int Selesai);

public record ReportResult(
    ReportGroup Total,
    List<ReportGroup> PerDivisi,
    List<ReportGroup> PerJenis,
    List<ReportGroup> PerPrioritas,
    List<ReportMonth> PerBulan);

// Perhitungan halaman Monitoring & Analisis Permintaan. Tanpa database, supaya mudah dites.
public static class LynReportCalculator
{
    // Tanggal & bulan dihitung dalam WIB (UTC+7), sama seperti nomor pengajuan.
    private static readonly TimeSpan Wib = TimeSpan.FromHours(7);

    public static ReportResult Build(IEnumerable<ReportRow> source, DateOnly today)
    {
        var rows = source.ToList();

        var perDivisi = rows
            .GroupBy(x => x.Division)
            .Select(g => Summarize(g.Key, g.Key, g, today))
            .OrderBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var perJenis = rows
            .GroupBy(x => (x.Division, x.Jenis))
            .Select(g => Summarize(g.Key.Division, g.Key.Jenis, g, today))
            .OrderBy(x => x.Division, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(x => x.Total)
            .ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var perPrioritas = rows
            .GroupBy(x => x.Request.Priority)
            .Select(g => Summarize("", g.Key, g, today))
            .OrderBy(x => LynPriority.Rank(x.Label))
            .ToList();

        var perBulan = rows
            .GroupBy(x => (x.Request.CreatedAt + Wib).ToString("yyyy-MM"))
            .Select(g => new ReportMonth(g.Key, g.Count(), g.Count(x => x.Request.Status == LynStatus.Selesai)))
            .OrderBy(x => x.Month, StringComparer.Ordinal)
            .ToList();

        return new ReportResult(Summarize("", "Total", rows, today), perDivisi, perJenis, perPrioritas, perBulan);
    }

    private static ReportGroup Summarize(string division, string label, IEnumerable<ReportRow> group, DateOnly today)
    {
        var requests = group.Select(x => x.Request).ToList();
        int Count(params string[] statuses) => requests.Count(r => statuses.Contains(r.Status));

        var finished = requests.Where(r => r.Status == LynStatus.Selesai && r.ClosedAt.HasValue).ToList();
        var withDeadline = finished.Where(r => r.NeededDate.HasValue).ToList();

        double? average = finished.Count == 0
            ? null
            : Math.Round(finished.Average(r => (r.ClosedAt!.Value - r.CreatedAt).TotalDays), 1, MidpointRounding.AwayFromZero);

        double? onTime = withDeadline.Count == 0
            ? null
            : Math.Round(withDeadline.Count(r => DateOnly.FromDateTime(r.ClosedAt!.Value + Wib) <= r.NeededDate!.Value) * 100.0 / withDeadline.Count,
                1, MidpointRounding.AwayFromZero);

        return new ReportGroup(
            division,
            label,
            requests.Count,
            Count(LynStatus.Baru),
            Count(LynStatus.Diterima, LynStatus.Dijadwalkan),
            Count(LynStatus.Diproses, LynStatus.MenungguInfo),
            Count(LynStatus.Selesai),
            Count(LynStatus.Ditolak, LynStatus.Dibatalkan),
            requests.Count(r => !LynStatus.Closed.Contains(r.Status) && r.NeededDate.HasValue && r.NeededDate.Value < today),
            average,
            onTime);
    }
}
