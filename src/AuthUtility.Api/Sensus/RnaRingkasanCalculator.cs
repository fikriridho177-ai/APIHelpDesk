namespace AuthUtility.Api.Sensus;

// Ringkasan "masalah" RNA per jenis sensus, untuk kartu di halaman divisi RNA.
// Tanpa database supaya mudah dites.
public record RnaJenisInfo(int Id, string Code, string Name, string Kategori, string Komoditas, string? Description,
    decimal? AmbangSedang, decimal? AmbangBerat, bool PunyaIndikator);

public record RnaHeaderRow(long Id, int JenisId, string Status, DateOnly Tanggal, string PersilSumber, string PersilId);

public record RnaJenisRingkasan(
    int Id, string Code, string Name, string Kategori, string Komoditas, string? Description,
    int SensusPeriode,          // semua sensus di periode (semua status)
    int MenungguApproval,       // SUBMITTED
    int Revisi,                 // REVISI
    int Disetujui,              // APPROVED
    int PersilDinilai,          // persil dengan sensus disetujui di periode
    int Berat, int Sedang, int Ringan, int Nihil,
    DateOnly? SensusTerakhir);

public static class RnaRingkasanCalculator
{
    // Tingkat per persil = nilai indikator pada sensus TERAKHIR yang disetujui untuk persil itu.
    public static List<RnaJenisRingkasan> Build(
        IEnumerable<RnaJenisInfo> jenis,
        IReadOnlyList<RnaHeaderRow> headers,
        IReadOnlyDictionary<long, decimal?> nilaiIndikator)
    {
        var byJenis = headers.GroupBy(h => h.JenisId).ToDictionary(g => g.Key, g => g.ToList());

        return jenis.Select(j =>
        {
            var rows = byJenis.GetValueOrDefault(j.Id) ?? new List<RnaHeaderRow>();

            var terakhirPerPersil = rows
                .Where(h => h.Status == AuthUtility.Domain.Entities.SnsStatus.Approved)
                .GroupBy(h => (h.PersilSumber, h.PersilId))
                .Select(g => g.OrderByDescending(h => h.Tanggal).ThenByDescending(h => h.Id).First())
                .ToList();

            var tingkat = terakhirPerPersil
                .Select(h => j.PunyaIndikator
                    ? SnsTingkat.Hitung(nilaiIndikator.GetValueOrDefault(h.Id), j.AmbangSedang, j.AmbangBerat)
                    : SnsTingkat.Kosong)
                .ToList();

            int Count(string t) => tingkat.Count(x => x == t);
            int Status(string s) => rows.Count(h => h.Status == s);

            return new RnaJenisRingkasan(
                j.Id, j.Code, j.Name, j.Kategori, j.Komoditas, j.Description,
                rows.Count,
                Status(AuthUtility.Domain.Entities.SnsStatus.Submitted),
                Status(AuthUtility.Domain.Entities.SnsStatus.Revisi),
                Status(AuthUtility.Domain.Entities.SnsStatus.Approved),
                terakhirPerPersil.Count,
                Count(SnsTingkat.Berat), Count(SnsTingkat.Sedang), Count(SnsTingkat.Ringan), Count(SnsTingkat.Nihil),
                rows.Count == 0 ? null : rows.Max(h => h.Tanggal));
        }).ToList();
    }
}
