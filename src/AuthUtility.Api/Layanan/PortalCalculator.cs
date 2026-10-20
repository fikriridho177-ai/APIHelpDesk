using AuthUtility.Domain.Entities;

namespace AuthUtility.Api.Layanan;

// Tiket + aksi yang boleh dilakukan user saat ini (hasil GetAllowedActions).
public record QueueCandidate(LynRequest Request, IReadOnlyCollection<string> AllowedActions);

public record DivisionKpi(string Division, int Masuk, int Terbuka, int Selesai, double? RataHariSelesai);

// Perhitungan widget Portal (Antrian & KPI Divisi). Tanpa database, supaya mudah dites.
public static class PortalCalculator
{
    public const string OtherDivision = "Lainnya";

    // Langkah berikutnya yang ditunggu dari petugas per status.
    private static readonly Dictionary<string, string> NextStep = new()
    {
        [LynStatus.Baru] = "ACCEPT",
        [LynStatus.Diterima] = "SCHEDULE",
        [LynStatus.Dijadwalkan] = "PROCESS",
    };

    private static int PriorityRank(string priority) => priority switch
    {
        "MENDESAK" => 0,
        "TINGGI" => 1,
        "NORMAL" => 2,
        "RENDAH" => 3,
        _ => 4
    };

    // Antrian: tiket orang lain yang menunggu langkah berikutnya dari user,
    // urut prioritas tertinggi lalu yang paling lama menunggu.
    public static (int Total, List<LynRequest> Items) SelectQueue(
        IEnumerable<QueueCandidate> candidates, int currentUserId, int take)
    {
        var matches = candidates
            .Where(c => c.Request.RequesterUserId != currentUserId &&
                        NextStep.TryGetValue(c.Request.Status, out var step) &&
                        c.AllowedActions.Contains(step))
            .Select(c => c.Request)
            .OrderBy(r => PriorityRank(r.Priority))
            .ThenBy(r => r.CreatedAt)
            .ToList();

        return (matches.Count, matches.Take(take).ToList());
    }

    // KPI per divisi untuk tiket yang dibuat sejak fromUtc.
    public static List<DivisionKpi> ComputeKpi(IEnumerable<(LynRequest Request, string? Division)> rows, DateTime fromUtc)
    {
        return rows
            .Where(x => x.Request.CreatedAt >= fromUtc)
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Division) ? OtherDivision : x.Division!)
            .Select(g =>
            {
                var requests = g.Select(x => x.Request).ToList();
                var finished = requests.Where(r => r.Status == LynStatus.Selesai).ToList();
                var durations = finished
                    .Where(r => r.ClosedAt.HasValue)
                    .Select(r => (r.ClosedAt!.Value - r.CreatedAt).TotalDays)
                    .ToList();

                return new DivisionKpi(
                    g.Key,
                    requests.Count,
                    requests.Count(r => !LynStatus.Closed.Contains(r.Status)),
                    finished.Count,
                    durations.Count == 0 ? null : Math.Round(durations.Average(), 1, MidpointRounding.AwayFromZero));
            })
            .OrderBy(x => x.Division, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
