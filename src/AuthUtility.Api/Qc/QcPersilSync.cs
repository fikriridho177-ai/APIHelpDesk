using AuthUtility.Api.Sensus;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Api.Qc;

// Baris master persil hasil pembacaan read-only dari SQL Server (sudah dipetakan).
public record QcPersilRaw(
    string Sumber, string PersilId, string PersilKode, string? NamaPersil, string Komoditas,
    string? WilayahKode, string? KodeDivisi, string? KodeAfdeling, string? Kcb,
    int? BulanTanam, int? TahunTanam, decimal? LuasHa, int? JumlahJalur, int? JumlahPohon,
    string? PemborongKode, string? PemborongNama);

public interface IQcPersilSourceReader
{
    Task<IReadOnlyList<QcPersilRaw>> ReadAsync(string sumber, CancellationToken ct = default);
}

// Adapter ke PersilReader yang sudah ada (SELECT saja ke MyPAD/RSUPPKB).
// Mapping kolom agronomis per sumber; nilai desimal sumber bisa ber-koma (id-ID).
public class QcPersilSourceReader(PersilReader reader) : IQcPersilSourceReader
{
    public async Task<IReadOnlyList<QcPersilRaw>> ReadAsync(string sumber, CancellationToken ct = default)
    {
        var source = PersilReader.Find(sumber)
            ?? throw new ArgumentException($"Sumber persil tidak dikenal: {sumber}.");
        var rows = await reader.ListAsync(source, null, null, true);
        var list = new List<QcPersilRaw>(rows.Count);
        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            var mapped = MapRow(source, row);
            if (mapped != null) list.Add(mapped);
        }
        return list;
    }

    // Dipisah supaya mapping kolom sumber bisa diuji tanpa koneksi SQL Server.
    public static QcPersilRaw? MapRow(PersilReader.Source source, PersilReader.Persil row)
    {
        var d = row.Detail;
        var id = Str(d, "PersilID") ?? row.PersilId;
        if (string.IsNullOrWhiteSpace(id)) return null;
        var kode = source.Key == "KLP" ? Str(d, "KodeSubPersil") ?? row.Kode : row.Kode;
        var tanam = source.Key switch
        {
            "TNM_NNS" => Date(d, "TglTanam"),
            "LHN_NNS" => Date(d, "S_Tanam"),
            _ => Date(d, "TglTanamKelapa")
        };
        return new QcPersilRaw(
            source.Key, id.Trim(), (kode ?? id).Trim(),
            Str(d, "Persil") ?? Str(d, "KodePersil"),
            source.Komoditas,
            Str(d, "KodeWilayah") ?? row.Wilayah,
            Str(d, "KodeDivisi"),
            Str(d, "KodeAfdeling") ?? row.Afdeling,
            Str(d, "KCB"),
            tanam?.Month, tanam?.Year,
            source.Key == "TNM_NNS" ? Dec(d, "HA") ?? Dec(d, "LuasLahan")
                : Dec(d, "LuasLahanKelapa") ?? Dec(d, "LuasLahan"),
            Int(d, source.Key is "TNM_NNS" or "LHN_NNS" ? "Jalur" : "Jumlah_Jalur")
                ?? Int(d, "JumlahBaris") ?? Int(d, "Baris"),
            source.Key == "TNM_NNS" ? FirstPositive(Int(d, "JumlahTanaman"), Int(d, "SisaTanaman"))
                : source.Key == "LHN_NNS" ? null
                : Int(d, "JumlahPohonKelapa"),
            Str(d, "KodePemborong"), Str(d, "Pemborong"));
    }

    private static string? Str(Dictionary<string, object?>? d, string key)
    {
        if (d == null || !d.TryGetValue(key, out var v) || v == null) return null;
        var s = Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture)?.Trim();
        return string.IsNullOrEmpty(s) ? null : s;
    }

    private static decimal? Dec(Dictionary<string, object?>? d, string key)
    {
        if (d == null || !d.TryGetValue(key, out var v) || v == null) return null;
        switch (v)
        {
            case decimal m: return m;
            case double x: return (decimal)x;
            case float x: return (decimal)x;
            case int i: return i;
            case long l: return l;
            default:
                var s = Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture)?.Trim();
                if (string.IsNullOrEmpty(s)) return null;
                s = s.Replace(',', '.');
                return decimal.TryParse(s, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
        }
    }

    private static int? Int(Dictionary<string, object?>? d, string key)
    {
        var m = Dec(d, key);
        return m == null ? null : (int)Math.Round(m.Value, MidpointRounding.AwayFromZero);
    }

    // Jumlah tanaman sumber bisa 0 bila belum diisi; sisa tanaman jadi cadangan.
    private static int? FirstPositive(params int?[] values)
    {
        foreach (var v in values)
            if (v is > 0) return v;
        return values.FirstOrDefault(v => v != null);
    }

    private static DateTime? Date(Dictionary<string, object?>? d, string key)
    {
        if (d == null || !d.TryGetValue(key, out var v) || v == null) return null;
        return v switch
        {
            DateTime t => t,
            DateOnly o => o.ToDateTime(TimeOnly.MinValue),
            string s when DateTime.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var t) => t,
            _ => null
        };
    }
}

public record QcSyncSourceResult(string Sumber, int Dibaca, int Baru, int Diperbarui, int Dinonaktifkan, DateTime SyncedAt);

// Upsert cache persil + pemborong temuan sumber. Idempotent: nilai sama tidak dihitung
// sebagai perubahan; persil yang hilang dari sumber dinonaktifkan (tidak dihapus).
public class QcPersilSyncService(AppDbContext db, IQcPersilSourceReader source)
{
    public async Task<IReadOnlyList<QcSyncSourceResult>> SyncAsync(string? sumber, string? userId, CancellationToken ct = default)
    {
        var keys = string.IsNullOrWhiteSpace(sumber)
            ? PersilReader.Sources.Keys.ToList()
            : new List<string> { sumber.Trim().ToUpperInvariant() };
        var results = new List<QcSyncSourceResult>();
        var now = DateTime.UtcNow;
        // Pemborong dipetakan sekali untuk seluruh sumber: penambahan antar sumber
        // belum ter-save sampai akhir sync, jadi tidak boleh cek DB per sumber.
        var pemborong = await db.QcPemborong.ToListAsync(ct);
        var pemborongByKode = pemborong.ToDictionary(x => x.Kode, StringComparer.OrdinalIgnoreCase);

        foreach (var key in keys)
        {
            var raws = await source.ReadAsync(key, ct);
            var existing = await db.QcPersils.Where(x => x.Sumber == key).ToListAsync(ct);
            var byId = existing.ToDictionary(x => x.PersilId);
            int baru = 0, ubah = 0, nonaktif = 0;
            var seen = new HashSet<string>();

            foreach (var raw in raws)
            {
                ct.ThrowIfCancellationRequested();
                seen.Add(raw.PersilId);
                if (!byId.TryGetValue(raw.PersilId, out var row))
                {
                    row = new QcPersil { Sumber = key, PersilId = raw.PersilId, CreatedAt = now };
                    db.QcPersils.Add(row);
                    byId[raw.PersilId] = row;
                    baru++;
                    Apply(row, raw, now);
                    continue;
                }
                if (Changed(row, raw) || !row.Active)
                {
                    Apply(row, raw, now);
                    ubah++;
                }
                else
                {
                    row.SyncedAt = now;
                }
            }

            foreach (var row in existing.Where(x => x.Active && !seen.Contains(x.PersilId)))
            {
                row.Active = false;
                row.SyncedAt = now;
                row.UpdatedAt = now;
                nonaktif++;
            }

            UpsertPemborong(raws, pemborongByKode, userId, now);
            results.Add(new QcSyncSourceResult(key, raws.Count, baru, ubah, nonaktif, now));
        }

        await db.SaveChangesAsync(ct);
        return results;
    }

    private static void Apply(QcPersil row, QcPersilRaw raw, DateTime now)
    {
        row.PersilKode = raw.PersilKode;
        row.NamaPersil = raw.NamaPersil;
        row.Komoditas = raw.Komoditas;
        row.WilayahKode = raw.WilayahKode;
        row.KodeDivisi = raw.KodeDivisi;
        row.KodeAfdeling = raw.KodeAfdeling;
        row.Kcb = raw.Kcb;
        row.BulanTanam = raw.BulanTanam;
        row.TahunTanam = raw.TahunTanam;
        row.LuasHa = raw.LuasHa;
        row.JumlahJalur = raw.JumlahJalur;
        row.JumlahPohon = raw.JumlahPohon;
        row.PemborongKode = raw.PemborongKode;
        row.PemborongNama = raw.PemborongNama;
        row.Active = true;
        row.SyncedAt = now;
        row.UpdatedAt = now;
    }

    private static bool Changed(QcPersil row, QcPersilRaw raw) =>
        row.PersilKode != raw.PersilKode || row.NamaPersil != raw.NamaPersil ||
        row.Komoditas != raw.Komoditas || row.WilayahKode != raw.WilayahKode ||
        row.KodeDivisi != raw.KodeDivisi || row.KodeAfdeling != raw.KodeAfdeling ||
        row.Kcb != raw.Kcb || row.BulanTanam != raw.BulanTanam || row.TahunTanam != raw.TahunTanam ||
        row.LuasHa != raw.LuasHa || row.JumlahJalur != raw.JumlahJalur || row.JumlahPohon != raw.JumlahPohon ||
        row.PemborongKode != raw.PemborongKode || row.PemborongNama != raw.PemborongNama;

    // Pemborong yang muncul di sumber ikut ter-cache; entri editan user (LOCAL) tidak ditimpa.
    private void UpsertPemborong(
        IReadOnlyList<QcPersilRaw> raws, Dictionary<string, QcPemborong> byKode, string? userId, DateTime now)
    {
        var found = raws
            .Where(x => !string.IsNullOrWhiteSpace(x.PemborongKode))
            .GroupBy(x => x.PemborongKode!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => (Kode: g.Key, Nama: g.Select(x => x.PemborongNama).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? g.Key))
            .ToList();
        foreach (var (kode, nama) in found)
        {
            if (byKode.TryGetValue(kode, out var row))
            {
                if (row.Sumber == "SYNC" && (row.Name != nama || !row.Active))
                {
                    row.Name = nama;
                    row.Active = true;
                    row.UpdatedBy = userId;
                    row.UpdatedAt = now;
                }
            }
            else
            {
                var added = new QcPemborong
                {
                    Kode = kode, Name = nama, Sumber = "SYNC", Active = true,
                    CreatedBy = userId, CreatedAt = now
                };
                db.QcPemborong.Add(added);
                byKode[kode] = added;
            }
        }
    }
}