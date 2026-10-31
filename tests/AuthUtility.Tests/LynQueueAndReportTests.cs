using AuthUtility.Api.Layanan;
using AuthUtility.Domain.Entities;

namespace AuthUtility.Tests;

public class LynQueueAndReportTests
{
    private static readonly DateTime T0 = new(2026, 10, 1, 1, 0, 0, DateTimeKind.Utc);

    /* ---------- Nomor antrian ---------- */

    private static QueueEntry Q(long id, int division, string status = LynStatus.Baru, string priority = "NORMAL", int minutes = 0) =>
        new(id, division, status, priority, T0.AddMinutes(minutes));

    [Fact]
    public void Queue_FirstComeFirstServed_PerDivision()
    {
        var result = LynRequestRules.QueuePositions([Q(1, 10, minutes: 0), Q(2, 20, minutes: 1), Q(3, 10, minutes: 2)]);

        Assert.Equal(1, result[1]);
        Assert.Equal(2, result[3]);
        Assert.Equal(1, result[2]); // divisi lain punya antrian sendiri
    }

    [Fact]
    public void Queue_HigherPriorityGoesFirst()
    {
        var result = LynRequestRules.QueuePositions([Q(1, 10, minutes: 0), Q(2, 10, priority: "URGENT", minutes: 5)]);

        Assert.Equal(1, result[2]);
        Assert.Equal(2, result[1]);
    }

    [Fact]
    public void Queue_OnlyWaitingStatusesGetNumber()
    {
        var result = LynRequestRules.QueuePositions([
            Q(1, 10, LynStatus.Diproses),
            Q(2, 10, LynStatus.Selesai),
            Q(3, 10, LynStatus.MenungguInfo),
            Q(4, 10, LynStatus.Dijadwalkan, minutes: 2),
            Q(5, 10, LynStatus.Diterima, minutes: 1),
            Q(6, 10, LynStatus.Baru, minutes: 3),
        ]);

        Assert.Equal(new long[] { 4, 5, 6 }, result.Keys.OrderBy(x => x));
        Assert.Equal(1, result[5]);
        Assert.Equal(2, result[4]);
        Assert.Equal(3, result[6]);
    }

    /* ---------- Kode jenis ---------- */

    [Theory]
    [InlineData("Drone – Foto udara", "DRONE_FOTO_UDARA")]
    [InlineData("  gis_peta  ", "GIS_PETA")]
    [InlineData("A--B__c", "A_B_C")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void NormalizeCode_UpperUnderscore(string? input, string expected)
    {
        Assert.Equal(expected, LynRequestRules.NormalizeCode(input));
    }

    [Fact]
    public void NormalizeCode_MaxLength()
    {
        Assert.Equal(LynRequestRules.CodeMaxLength, LynRequestRules.NormalizeCode(new string('A', 80)).Length);
    }

    /* ---------- Laporan Monitoring & Analisis ---------- */

    private static readonly DateOnly Today = new(2026, 10, 21);

    private static ReportRow R(string division, string jenis, string status, string priority = "NORMAL",
        DateTime? created = null, DateTime? closed = null, DateOnly? needed = null) =>
        new(new LynRequest
        {
            Status = status,
            Priority = priority,
            CreatedAt = created ?? T0,
            ClosedAt = closed,
            NeededDate = needed
        }, division, jenis);

    [Fact]
    public void Report_CountsStatusBuckets()
    {
        var report = LynReportCalculator.Build([
            R("GIS", "Peta", LynStatus.Baru),
            R("GIS", "Peta", LynStatus.Diterima),
            R("GIS", "Peta", LynStatus.Dijadwalkan),
            R("GIS", "Peta", LynStatus.Diproses),
            R("GIS", "Peta", LynStatus.MenungguInfo),
            R("GIS", "Peta", LynStatus.Selesai, closed: T0.AddDays(1)),
            R("GIS", "Peta", LynStatus.Ditolak),
            R("GIS", "Peta", LynStatus.Dibatalkan),
        ], Today);

        var t = report.Total;
        Assert.Equal(8, t.Total);
        Assert.Equal(1, t.MenungguApproval);
        Assert.Equal(2, t.Disetujui);
        Assert.Equal(2, t.DalamProses);
        Assert.Equal(1, t.Selesai);
        Assert.Equal(2, t.DitolakBatal);
    }

    [Fact]
    public void Report_LateOnlyCountsOpenTicketsPastDeadline()
    {
        var report = LynReportCalculator.Build([
            R("GIS", "Peta", LynStatus.Baru, needed: Today.AddDays(-1)),       // terlambat
            R("GIS", "Peta", LynStatus.Diproses, needed: Today),                // deadline hari ini, belum terlambat
            R("GIS", "Peta", LynStatus.Selesai, needed: Today.AddDays(-5), closed: T0),
            R("GIS", "Peta", LynStatus.Baru),                                   // tanpa deadline
        ], Today);

        Assert.Equal(1, report.Total.Terlambat);
    }

    [Fact]
    public void Report_AverageDaysAndOnTimePercent()
    {
        var report = LynReportCalculator.Build([
            // selesai 2 hari, tepat waktu (deadline 5 Okt)
            R("GIS", "Peta", LynStatus.Selesai, created: T0, closed: T0.AddDays(2), needed: new DateOnly(2026, 10, 5)),
            // selesai 4 hari, terlambat (deadline 2 Okt)
            R("GIS", "Peta", LynStatus.Selesai, created: T0, closed: T0.AddDays(4), needed: new DateOnly(2026, 10, 2)),
        ], Today);

        Assert.Equal(3.0, report.Total.RataHariSelesai);
        Assert.Equal(50.0, report.Total.PersenTepatWaktu);
    }

    [Fact]
    public void Report_OnTimeUsesLocalDate()
    {
        // Selesai 2 Okt 20:00 UTC = 3 Okt 03:00 WIB -> lewat deadline 2 Okt.
        var report = LynReportCalculator.Build([
            R("GIS", "Peta", LynStatus.Selesai, created: T0, closed: new DateTime(2026, 10, 2, 20, 0, 0, DateTimeKind.Utc), needed: new DateOnly(2026, 10, 2)),
        ], Today);

        Assert.Equal(0.0, report.Total.PersenTepatWaktu);
    }

    [Fact]
    public void Report_NoFinishedTickets_NullAverages()
    {
        var report = LynReportCalculator.Build([R("GIS", "Peta", LynStatus.Baru)], Today);

        Assert.Null(report.Total.RataHariSelesai);
        Assert.Null(report.Total.PersenTepatWaktu);
    }

    [Fact]
    public void Report_GroupsPerDivisionJenisPriorityAndMonth()
    {
        var report = LynReportCalculator.Build([
            R("QC", "Audit", LynStatus.Baru, "URGENT", created: new DateTime(2026, 9, 30, 18, 0, 0, DateTimeKind.Utc)), // 1 Okt WIB
            R("GIS", "Peta", LynStatus.Baru, created: new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc)),
            R("GIS", "Peta", LynStatus.Baru, "URGENT"),
            R("GIS", "Drone", LynStatus.Baru),
        ], Today);

        Assert.Equal(new[] { "GIS", "QC" }, report.PerDivisi.Select(x => x.Label));
        Assert.Equal(3, report.PerDivisi[0].Total);

        // Per jenis: urut divisi, lalu jumlah terbanyak.
        Assert.Equal(new[] { ("GIS", "Peta", 2), ("GIS", "Drone", 1), ("QC", "Audit", 1) },
            report.PerJenis.Select(x => (x.Division, x.Label, x.Total)));

        // Per prioritas: Urgent lebih dulu.
        Assert.Equal(new[] { ("URGENT", 2), ("NORMAL", 2) }, report.PerPrioritas.Select(x => (x.Label, x.Total)));

        Assert.Equal(new[] { ("2026-09", 1), ("2026-10", 3) }, report.PerBulan.Select(x => (x.Month, x.Masuk)));
    }

    [Fact]
    public void Report_EmptyRows()
    {
        var report = LynReportCalculator.Build([], Today);

        Assert.Equal(0, report.Total.Total);
        Assert.Empty(report.PerDivisi);
        Assert.Empty(report.PerBulan);
    }
}
