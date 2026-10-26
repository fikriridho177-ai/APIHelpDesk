using System.Text;
using AuthUtility.Domain.Entities;

namespace AuthUtility.Api.Layanan;

// Data minimal tiket untuk menghitung nomor antrian. DivisionKey = id menu divisi.
public record QueueEntry(long Id, int DivisionKey, string Status, string Priority, DateTime CreatedAt);

// Aturan isian form pengajuan (mengikuti form permintaan GIS). Tanpa database, supaya mudah dites.
public static class LynRequestRules
{
    public const int TitleMaxLength = 200;
    public const int CodeMaxLength = 50;

    // Nomor antrian per divisi untuk tiket yang belum mulai dikerjakan (Baru / Disetujui / Dijadwalkan).
    // Urutan sama dengan widget Antrian: prioritas tertinggi dulu, lalu yang paling lama menunggu.
    public static Dictionary<long, int> QueuePositions(IEnumerable<QueueEntry> entries)
    {
        var result = new Dictionary<long, int>();

        foreach (var division in entries.Where(x => LynStatus.Waiting.Contains(x.Status)).GroupBy(x => x.DivisionKey))
        {
            var position = 0;
            foreach (var entry in division.OrderBy(x => LynPriority.Rank(x.Priority)).ThenBy(x => x.CreatedAt).ThenBy(x => x.Id))
                result[entry.Id] = ++position;
        }

        return result;
    }

    // Kode master jenis: huruf besar, selain huruf/angka jadi "_", mis. "Drone – Foto udara" -> "DRONE_FOTO_UDARA".
    public static string NormalizeCode(string? input)
    {
        var builder = new StringBuilder();
        foreach (var ch in (input ?? "").Trim().ToUpperInvariant())
        {
            var isWord = ch is >= 'A' and <= 'Z' or >= '0' and <= '9';
            if (isWord) builder.Append(ch);
            else if (builder.Length > 0 && builder[^1] != '_') builder.Append('_');
        }

        var code = builder.ToString().TrimEnd('_');
        return code.Length > CodeMaxLength ? code[..CodeMaxLength].TrimEnd('_') : code;
    }

    // Form tidak punya isian judul: judul = jenis permintaan + lokasi, mis. "Pembuatan Peta - persil 17-01".
    public static string BuildTitle(string serviceName, string? otherType, string? location)
    {
        var jenis = string.IsNullOrWhiteSpace(otherType) ? serviceName.Trim() : otherType.Trim();
        var title = string.IsNullOrWhiteSpace(location) ? jenis : $"{jenis} - {location.Trim()}";
        return title.Length > TitleMaxLength ? title[..TitleMaxLength] : title;
    }

    // Perusahaan yang dicentang + isian "Yang lain" -> "PT A; PT B; Lainnya". Kosong & dobel dibuang.
    public static string JoinCompanies(IEnumerable<string>? companies, string? other)
    {
        var all = (companies ?? []).Append(other ?? "")
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        return string.Join("; ", all);
    }
}
