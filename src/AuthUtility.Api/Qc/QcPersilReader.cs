using System.Globalization;

namespace AuthUtility.Api.Sensus;

// QC uses MyPAD views; the existing Sensus table sources remain unchanged.
public partial class PersilReader
{
    public record QcPersil(string PersilId, string PersilKode, string? Wilayah, string? Afdeling, string? Subpersil);

    public static Source QcSource(string commodity) => commodity.Trim().ToUpperInvariant() switch
    {
        "NANAS" => new("vwPersilNNS", "Persil Nanas", "MyPAD", "vwPersilNNS", "", true, "NANAS"),
        "KELAPA" => new("vwMstPersilKelapa", "Persil Kelapa", "MyPAD", "vwMstPersilKelapa", "", true, "KELAPA"),
        _ => throw new ArgumentException("Komoditas QC harus NANAS atau KELAPA.", nameof(commodity))
    };

    public Task<List<QcPersil>> ListQcAsync(Source source) =>
        QueryAsync(source, $"SELECT TOP ({MaxRows + 1}) * FROM [dbo].[{source.Table}]", new(),
            reader => MapQcPersil(ReadAll(reader)));

    public static QcPersil MapQcPersil(IReadOnlyDictionary<string, object?> values)
    {
        var row = new Dictionary<string, object?>(values, StringComparer.OrdinalIgnoreCase);
        string? Get(params string[] columns) => columns
            .Select(column => row.TryGetValue(column, out var value) && value != null && value != DBNull.Value
                ? Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() : null)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        var persil = Get("KodePersil", "Persil", "PersilKode", "NamaPersil", "NoPersil", "Kode");
        var subpersil = Get("KodeSubPersil", "SubPersil", "SubPersilKode", "NamaSubPersil");
        persil ??= subpersil;
        if (string.IsNullOrEmpty(persil))
            throw new InvalidOperationException("View MyPAD tidak menyediakan kolom Persil/KodePersil.");

        var wilayah = Get("KodeWilayah", "Wilayah", "WilayahKode");
        var afdeling = Get("KodeAfdeling", "Afdeling", "AfdelingKode", "Divisi", "Div");
        // Some view versions have no PersilID. Use all location components to avoid collisions.
        var id = Get("PersilID", "IDPersil", "Id") ?? string.Join("|", wilayah, afdeling, persil, subpersil);
        return new(id, persil, wilayah, afdeling, subpersil);
    }
}
