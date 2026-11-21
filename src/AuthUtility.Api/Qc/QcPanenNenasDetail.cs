using System.Text.Json;
using System.Text.Json.Nodes;
using AuthUtility.Domain.Entities;

namespace AuthUtility.Api.Qc;

// Detail typed Panen Nenas: parse/validasi payload grid, pemisahan samples
// dari DataJson (DataJson hanya menyimpan ekstra header seperti umurPanen),
// dan penggabungan kembali baris typed saat detail dibaca.
// Rumus total/% TIDAK di sini — dihitung di view vwQcPanenNenasDetail/Rekap
// dan live di grid frontend dengan semantik yang sama.
public static class QcPanenNenasDetail
{
    public const string FormCode = "QC-PANEN-NNS";
    public const int MaxRows = 20;

    public static bool TryParse(JsonElement? data, out List<QcInspSampelPanenNenas> rows, out string? error)
    {
        rows = new List<QcInspSampelPanenNenas>();
        error = null;
        if (data is not JsonElement root || root.ValueKind != JsonValueKind.Object) return true;
        if (!root.TryGetProperty("samples", out var samples) || samples.ValueKind == JsonValueKind.Null) return true;
        if (samples.ValueKind != JsonValueKind.Array)
        {
            error = "Format samples panen nanas tidak valid.";
            return false;
        }
        if (samples.GetArrayLength() > MaxRows)
        {
            error = $"Template Panen Nenas menampung maksimal {MaxRows} baris sampel.";
            return false;
        }

        var index = 0;
        foreach (var item in samples.EnumerateArray())
        {
            index++;
            if (item.ValueKind != JsonValueKind.Object)
            {
                error = $"Baris sampel {index} tidak valid.";
                return false;
            }

            var jalur = ReadText(item, "nomorJalur");
            if (string.IsNullOrWhiteSpace(jalur))
            {
                error = $"Nomor jalur sampel baris {index} wajib diisi.";
                return false;
            }
            if (jalur.Trim().Length > 50)
            {
                error = $"Nomor jalur sampel baris {index} maksimal 50 karakter.";
                return false;
            }

            var row = new QcInspSampelPanenNenas { RowNo = index, NomorJalur = jalur.Trim() };
            var counts = new (string Key, string Label, Action<int> Set)[]
            {
                ("jumlahPohon", "Jumlah pohon sampel", v => row.JumlahPohon = v),
                ("buahTidakDipetik", "Buah tidak dipetik", v => row.BuahTidakDipetik = v),
                ("buahTelahDipetik", "Buah telah dipetik", v => row.BuahTelahDipetik = v),
                ("buahTerpotong", "Buah terpotong", v => row.BuahTerpotong = v),
            };
            foreach (var (key, label, set) in counts)
            {
                if (!TryReadInt(item, key, out var value))
                {
                    error = $"{label} baris {index} harus diisi dengan bilangan bulat nol atau lebih.";
                    return false;
                }
                set(value);
            }

            var keterangan = ReadText(item, "keterangan");
            if (keterangan != null && keterangan.Trim().Length > 250)
            {
                error = $"Keterangan baris {index} maksimal 250 karakter.";
                return false;
            }
            row.Keterangan = string.IsNullOrWhiteSpace(keterangan) ? null : keterangan.Trim();
            rows.Add(row);
        }
        return true;
    }

    // DataJson tanpa samples (samples pindah ke tabel typed).
    public static string? StripSamples(string? dataJson)
    {
        if (string.IsNullOrWhiteSpace(dataJson)) return null;
        if (JsonNode.Parse(dataJson) is not JsonObject obj) return dataJson;
        obj.Remove("samples");
        return obj.ToJsonString();
    }

    // Detail dibaca frontend sebagai data.samples seperti form lain.
    public static JsonDocument? WithSamples(string? dataJson, IReadOnlyList<QcInspSampelPanenNenas> rows)
    {
        var obj = string.IsNullOrWhiteSpace(dataJson)
            ? new JsonObject()
            : JsonNode.Parse(dataJson) as JsonObject ?? new JsonObject();
        obj["samples"] = new JsonArray(rows.Select(r => (JsonNode)new JsonObject
        {
            ["nomorJalur"] = r.NomorJalur,
            ["jumlahPohon"] = r.JumlahPohon,
            ["buahTidakDipetik"] = r.BuahTidakDipetik,
            ["buahTelahDipetik"] = r.BuahTelahDipetik,
            ["buahTerpotong"] = r.BuahTerpotong,
            ["keterangan"] = r.Keterangan,
        }).ToArray());
        return JsonDocument.Parse(obj.ToJsonString());
    }

    private static string? ReadText(JsonElement obj, string key)
    {
        if (!obj.TryGetProperty(key, out var value) || value.ValueKind == JsonValueKind.Null) return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        };
    }

    private static bool TryReadInt(JsonElement obj, string key, out int value)
    {
        value = 0;
        if (!obj.TryGetProperty(key, out var raw) || raw.ValueKind == JsonValueKind.Null) return false;
        if (raw.ValueKind == JsonValueKind.Number)
            return raw.TryGetInt32(out value) && value >= 0;
        if (raw.ValueKind == JsonValueKind.String)
            return int.TryParse(raw.GetString()?.Trim(), System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out value) && value >= 0;
        return false;
    }
}