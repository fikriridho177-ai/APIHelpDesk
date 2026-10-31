using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text.Json.Serialization;
using AuthUtility.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Api.Sensus;

// Membaca master persil dari MyPAD & RSUPPKB (SQL Server). HANYA SELECT.
//   KLP     -> MyPAD   tblMstPersilKelapa              (KodeSubPersil, KodeAfdeling)  komoditas KELAPA
//   TNM_NNS -> MyPAD   tblNNSTrnInventarisTanamanNenas (Kode)                         komoditas NANAS
//   LHN_NNS -> MyPAD   tblNNSTrnInventarisLahanNenas   (Kode)                         komoditas NANAS
//   PNN     -> RSUPPKB tblMstPersilKelapa              (Kode)                         komoditas KELAPA
public class PersilReader(MyPADDbContext myPad, RSUPPKBDbContext rsuppkb)
{
    public const int MaxRows = 20000;

    public record Source(string Key, string Label, string Database, string Table, string CodeColumn, bool HasAfdeling, string Komoditas);

    // Detail = semua kolom tabel sumber (nama kolom asli -> nilai), hanya diisi bila ListAsync(..., withDetail: true).
    public record Persil(string Sumber, string Kode, string? Afdeling, string? Wilayah, double? Latitude, double? Longitude, string? PersilId,
        [property: JsonPropertyName("persil"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PersilKode = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Dictionary<string, object?>? Detail = null);

    // Kolom yang dianggap "persil" (diambil yang pertama ada di tabel).
    private static readonly string[] PersilColumns = ["KodePersil", "Persil", "NamaPersil", "NoPersil"];

    public static readonly IReadOnlyDictionary<string, Source> Sources = new Dictionary<string, Source>(StringComparer.OrdinalIgnoreCase)
    {
        ["KLP"] = new("KLP", "Persil Kelapa", "MyPAD", "tblMstPersilKelapa", "KodeSubPersil", true, "KELAPA"),
        ["TNM_NNS"] = new("TNM_NNS", "Tanaman Nanas", "MyPAD", "tblNNSTrnInventarisTanamanNenas", "Kode", false, "NANAS"),
        ["LHN_NNS"] = new("LHN_NNS", "Lahan Nanas", "MyPAD", "tblNNSTrnInventarisLahanNenas", "Kode", false, "NANAS"),
        ["PNN"] = new("PNN", "Persil Kelapa RSUPPKB", "RSUPPKB", "tblMstPersilKelapa", "Kode", false, "KELAPA"),
    };

    public static Source? Find(string? key) => Sources.TryGetValue(key ?? "", out var s) ? s : null;

    // Daftar persil aktif. Mengembalikan MaxRows + 1 baris bila data lebih banyak (untuk tanda "terpotong").
    // withDetail = SELECT * (semua kolom ke Persil.Detail) - dipakai halaman Master; mobile tetap ringkas.
    public Task<List<Persil>> ListAsync(Source source, string? wilayah = null, string? search = null, bool withDetail = false)
    {
        var where = "NotActive = 0";
        var parameters = new Dictionary<string, object>();

        if (!string.IsNullOrWhiteSpace(wilayah))
        {
            where += " AND KodeWilayah = @wilayah";
            parameters["@wilayah"] = wilayah.Trim();
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            where += $" AND ({source.CodeColumn} LIKE @search OR CAST(PersilID AS varchar(100)) LIKE @search)";
            parameters["@search"] = $"%{search.Trim()}%";
        }

        return QueryPersil(source, $"TOP ({MaxRows + 1})", where, parameters, withDetail);
    }

    // Satu persil aktif berdasarkan PersilID (dipakai validasi kiriman Android).
    public async Task<Persil?> FindAsync(Source source, string persilId)
    {
        var rows = await QueryPersil(source, "TOP (1)", "NotActive = 0 AND CAST(PersilID AS varchar(100)) = @persilId",
            new() { ["@persilId"] = persilId.Trim() });
        return rows.FirstOrDefault();
    }

    public Task<List<string>> WilayahAsync(Source source) =>
        QueryAsync(source, $@"
            SELECT DISTINCT KodeWilayah
            FROM {source.Table}
            WHERE NotActive = 0 AND KodeWilayah IS NOT NULL AND LTRIM(RTRIM(KodeWilayah)) <> ''
            ORDER BY KodeWilayah", new(), reader => Text(reader["KodeWilayah"])!);

    /* =========================================================
       HELPER
    ========================================================= */

    private Task<List<Persil>> QueryPersil(Source source, string top, string where, Dictionary<string, object> parameters, bool withDetail = false)
    {
        var columns = withDetail
            ? $"t.*, t.{source.CodeColumn} AS Kode"
            : (source.HasAfdeling ? $"{source.CodeColumn} AS Kode, KodeAfdeling" : $"{source.CodeColumn} AS Kode") + ", KodeWilayah, Latitude, Longitude, PersilID";

        var sql = $@"
            SELECT {top} {columns}
            FROM {source.Table} t
            WHERE {where}
            ORDER BY t.KodeWilayah, t.{source.CodeColumn}";

        return QueryAsync(source, sql, parameters, reader =>
        {
            var detail = withDetail ? ReadAll(reader) : null;

            return new Persil(
                source.Key,
                Text(reader["Kode"]) ?? "",
                source.HasAfdeling ? Text(reader["KodeAfdeling"]) : null,
                Text(reader["KodeWilayah"]),
                Number(reader["Latitude"]),
                Number(reader["Longitude"]),
                Text(reader["PersilID"]),
                detail == null ? null : PersilColumns.Where(detail.ContainsKey).Select(c => Text(detail[c] ?? DBNull.Value)).FirstOrDefault(),
                detail);
        });
    }

    // Semua kolom satu baris -> dictionary (kunci = nama kolom asli). Kolom biner / tipe khusus dilewati.
    private static Dictionary<string, object?> ReadAll(DbDataReader reader)
    {
        var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < reader.FieldCount; i++)
        {
            var name = reader.GetName(i);
            if (string.IsNullOrEmpty(name)) continue;

            try
            {
                var value = reader.GetValue(i);
                if (value is byte[]) continue;
                row.TryAdd(name, value == DBNull.Value ? null : value is string s ? s.Trim() : value);
            }
            catch (Exception)
            {
                // Tipe yang tidak bisa dibaca (mis. geography) tidak ditampilkan.
            }
        }

        return row;
    }

    private async Task<List<T>> QueryAsync<T>(Source source, string sql, Dictionary<string, object> parameters, Func<DbDataReader, T> map)
    {
        DbContext context = source.Database == "RSUPPKB" ? rsuppkb : myPad;
        var connection = context.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open) await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        await using var reader = await command.ExecuteReaderAsync();
        var result = new List<T>();
        while (await reader.ReadAsync()) result.Add(map(reader));
        return result;
    }

    private static string? Text(object value) =>
        value == DBNull.Value ? null : Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim();

    // Latitude / Longitude bisa tersimpan sebagai angka atau teks (titik maupun koma desimal). 0 = belum diisi.
    private static double? Number(object value)
    {
        if (value == DBNull.Value) return null;

        if (value is IConvertible && value is not string)
        {
            var number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            return number == 0 ? null : number;
        }

        var text = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim().Replace(',', '.');
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && parsed != 0 ? parsed : null;
    }
}
