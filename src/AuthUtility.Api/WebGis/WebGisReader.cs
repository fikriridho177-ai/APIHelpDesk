using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Npgsql;

namespace AuthUtility.Api.WebGis;

// Pembaca database PostGIS milik WebGIS_PT_RSUP (aplikasi GIS tim GIS).
// Hanya SELECT: koneksi dipaksa read-only, jadi database WebGIS tidak pernah diubah dari sini.
// Query katalog & tile disalin dari WebGIS (src/services/layers/catalog.ts) agar hasilnya sama.
//
// Konfigurasi (appsettings):
//   ConnectionStrings:WebGis  -> mis. "Host=...;Port=5432;Database=company_gis;Username=gis_app;Password=..."
//   WebGis:StorageRoot        -> folder GIS_STORAGE_ROOT WebGIS (opsional, untuk tile raster GeoTIFF/GeoPDF)
public sealed partial class WebGisReader : IAsyncDisposable
{
    public const string ManagedRasterMarker = "portal-raster-v1";

    // Batas sama dengan WebGIS (vectorTileLimits).
    private const int TileCandidates = 10_000;
    private const int TileBytes = 2 * 1024 * 1024;
    private const int TileTimeoutMs = 5_000;

    private readonly NpgsqlDataSource? dataSource;

    public WebGisReader(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("WebGis");
        StorageRoot = configuration["WebGis:StorageRoot"] is { Length: > 0 } root ? Path.GetFullPath(root) : null;
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            // Setiap transaksi read-only: INSERT/UPDATE/DELETE/DDL ditolak oleh PostgreSQL.
            Options = "-c default_transaction_read_only=on",
            ApplicationName = "HelpDesk-WebGisReader",
        };
        dataSource = NpgsqlDataSource.Create(builder.ConnectionString);
    }

    public bool Configured => dataSource != null;
    public string? StorageRoot { get; }
    public bool RasterAvailable => StorageRoot != null;

    public async Task<(bool Connected, string? Message)> CheckAsync(CancellationToken ct)
    {
        if (dataSource == null) return (false, "ConnectionStrings:WebGis belum diisi di appsettings API.");
        try
        {
            await using var cmd = dataSource.CreateCommand("SELECT count(*) FROM app.layers");
            await cmd.ExecuteScalarAsync(ct);
            return (true, null);
        }
        catch (Exception error) when (error is NpgsqlException or PostgresException)
        {
            return (false, $"Database WebGIS tidak dapat dibaca: {error.Message}");
        }
    }

    /* ---------- Katalog layer (setara listLayers untuk VIEWER) ---------- */

    public async Task<List<JsonObject>> ListLayersAsync(CancellationToken ct)
    {
        var source = Require();
        const string sql = """
            SELECT id, name, description, layer_type::text, source_type::text, storage_metadata::text,
                   geometry_type, feature_count, style_json::text, is_visible, default_visible, group_name,
                   sort_order, updated_at,
                   ST_XMin(bbox::box3d), ST_YMin(bbox::box3d), ST_XMax(bbox::box3d), ST_YMax(bbox::box3d)
            FROM app.layers
            WHERE state = 'READY' AND is_visible = true
              AND (layer_type = 'VECTOR'
                   OR (layer_type = 'RASTER'
                       AND storage_metadata->>'managedBy' = @marker
                       AND storage_metadata->'raster'->>'stage' = 'READY'))
            ORDER BY sort_order, created_at, id
            """;
        await using var cmd = source.CreateCommand(sql);
        cmd.Parameters.AddWithValue("marker", ManagedRasterMarker);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var result = new List<JsonObject>();
        while (await reader.ReadAsync(ct))
        {
            var layerType = reader.GetString(3);
            // Tile raster dibaca dari folder storage WebGIS; tanpa StorageRoot layer raster tidak ditampilkan.
            if (layerType == "RASTER" && !RasterAvailable) continue;

            var item = new JsonObject
            {
                ["id"] = reader.GetGuid(0).ToString(),
                ["name"] = reader.GetString(1),
                ["description"] = reader.GetString(2),
                ["layerType"] = layerType,
                ["bounds"] = new JsonArray(reader.GetDouble(14), reader.GetDouble(15), reader.GetDouble(16), reader.GetDouble(17)),
                ["isVisible"] = reader.GetBoolean(9),
                ["defaultVisible"] = reader.GetBoolean(10),
                ["groupName"] = reader.IsDBNull(11) ? null : reader.GetString(11),
                ["sortOrder"] = reader.GetInt32(12),
                ["updatedAt"] = reader.GetFieldValue<DateTime>(13).ToUniversalTime().ToString("O"),
                // Style mentah; frontend menormalkan dengan kode WebGIS (normalizeVectorStyle).
                ["style"] = JsonNode.Parse(reader.GetString(8)),
            };

            if (layerType == "RASTER")
            {
                // Allowlist: artifactId / path storage tidak pernah dikirim ke browser.
                var raster = JsonNode.Parse(reader.GetString(5))?["raster"];
                if (raster == null) continue;
                item["sourceType"] = reader.GetString(4);
                item["raster"] = new JsonObject
                {
                    ["width"] = raster["width"]?.DeepClone(),
                    ["height"] = raster["height"]?.DeepClone(),
                    ["bandCount"] = raster["bandCount"]?.DeepClone(),
                    ["sourceSrid"] = raster["sourceSrid"]?.DeepClone(),
                    ["minZoom"] = raster["minZoom"]?.DeepClone(),
                    ["maxZoom"] = raster["maxZoom"]?.DeepClone(),
                    ["selectedPage"] = raster["selectedPage"]?.DeepClone(),
                };
            }
            else
            {
                if (reader.IsDBNull(6)) continue;
                item["geometryType"] = reader.GetString(6);
                item["featureCount"] = reader.IsDBNull(7) ? 0 : reader.GetInt64(7);
            }

            result.Add(item);
        }

        return result;
    }

    /* ---------- Vector tile (MVT), setara getVectorTile ---------- */

    public async Task<TileResult> GetVectorTileAsync(Guid id, int z, int x, int y, CancellationToken ct)
    {
        var source = Require();
        await using var conn = await source.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        await using (var timeout = new NpgsqlCommand($"SET LOCAL statement_timeout = {TileTimeoutMs}", conn, tx))
            await timeout.ExecuteNonQueryAsync(ct);

        string? tableName;
        await using (var find = new NpgsqlCommand(
            "SELECT table_name FROM app.layers WHERE id = @id AND state = 'READY' AND layer_type = 'VECTOR' AND is_visible = true",
            conn, tx))
        {
            find.Parameters.AddWithValue("id", id);
            tableName = await find.ExecuteScalarAsync(ct) as string;
        }

        // Nama tabel hanya dari registry WebGIS dan wajib berpola layer_<32 hex>.
        if (tableName == null || !LayerTablePattern().IsMatch(tableName)) return TileResult.NotFound;

        var sql = $"""
            WITH bounds AS (SELECT ST_TileEnvelope(@z, @x, @y) AS geom),
            candidates AS MATERIALIZED (
              SELECT source.* FROM gis."{tableName}" AS source CROSS JOIN bounds
              WHERE source.geom && ST_Transform(ST_Expand(bounds.geom, (ST_XMax(bounds.geom)-ST_XMin(bounds.geom))*64/4096),4326)
              LIMIT @limit
            ), total AS (SELECT count(*)::integer AS feature_count FROM candidates),
            features AS (
              SELECT feature_id, properties, ST_AsMVTGeom(ST_Transform(source.geom,3857),bounds.geom,4096,64,true) AS geom
              FROM candidates AS source CROSS JOIN bounds CROSS JOIN total
              WHERE total.feature_count <= @candidates
              ORDER BY feature_id
            ), encoded AS MATERIALIZED (
              SELECT ST_AsMVT(features,'features',4096,'geom','feature_id') AS tile FROM features WHERE geom IS NOT NULL
            ) SELECT total.feature_count, octet_length(encoded.tile) AS tile_bytes,
              CASE WHEN octet_length(encoded.tile) <= @bytes THEN encoded.tile ELSE NULL END AS tile
              FROM encoded CROSS JOIN total
            """;

        try
        {
            await using var cmd = new NpgsqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("z", z);
            cmd.Parameters.AddWithValue("x", x);
            cmd.Parameters.AddWithValue("y", y);
            cmd.Parameters.AddWithValue("limit", TileCandidates + 1);
            cmd.Parameters.AddWithValue("candidates", TileCandidates);
            cmd.Parameters.AddWithValue("bytes", TileBytes);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return TileResult.Empty;

            var count = reader.GetInt32(0);
            if (count > TileCandidates) return TileResult.TooDense;
            if (reader.IsDBNull(2)) return reader.IsDBNull(1) ? TileResult.Empty : TileResult.TooLarge;
            return TileResult.Ok(reader.GetFieldValue<byte[]>(2));
        }
        catch (PostgresException error) when (error.SqlState == "57014")
        {
            return TileResult.Timeout;
        }
    }

    /* ---------- Raster tile (PNG dari folder storage), setara getRasterTile ---------- */

    // PNG transparan 1x1: area tanpa data tidak menjadi kotak putih.
    private static readonly byte[] TransparentTile = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNgYAAAAAMAASsJTYQAAAAASUVORK5CYII=");
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

    public async Task<TileResult> GetRasterTileAsync(Guid id, int z, int x, int y, CancellationToken ct)
    {
        var source = Require();
        if (StorageRoot == null) return TileResult.NotFound;

        await using var cmd = source.CreateCommand("""
            SELECT storage_metadata::text FROM app.layers
            WHERE id = @id AND layer_type = 'RASTER' AND state = 'READY' AND is_visible = true
            """);
        cmd.Parameters.AddWithValue("id", id);
        if (await cmd.ExecuteScalarAsync(ct) is not string metadataText) return TileResult.NotFound;

        var metadata = JsonNode.Parse(metadataText);
        var raster = metadata?["raster"];
        if (metadata?["managedBy"]?.GetValue<string>() != ManagedRasterMarker ||
            raster?["stage"]?.GetValue<string>() != "READY" ||
            !Guid.TryParse(raster["artifactId"]?.GetValue<string>(), out var artifactId))
            return TileResult.NotFound;

        var minZoom = raster["minZoom"]?.GetValue<int>() ?? 0;
        var maxZoom = raster["maxZoom"]?.GetValue<int>() ?? 22;
        if (z < minZoom || z > maxZoom) return TileResult.Invalid;

        // <root>/rasters/<layerId>/<artifactId>/tiles/<z>/<x>/<y>.png (lihat WebGIS src/server/storage/raster.ts).
        var path = Path.GetFullPath(Path.Combine(StorageRoot, "rasters", id.ToString("D"), artifactId.ToString("D"),
            "tiles", z.ToString(), x.ToString(), $"{y}.png"));
        if (!path.StartsWith(StorageRoot, StringComparison.OrdinalIgnoreCase)) return TileResult.NotFound;

        var info = new FileInfo(path);
        if (!info.Exists) return TileResult.Ok(TransparentTile);
        if (info.Length < 8 || info.Length > 1024 * 1024) return TileResult.NotFound;

        var data = await File.ReadAllBytesAsync(path, ct);
        return data.AsSpan(0, 8).SequenceEqual(PngSignature) ? TileResult.Ok(data) : TileResult.NotFound;
    }

    private NpgsqlDataSource Require() =>
        dataSource ?? throw new InvalidOperationException("ConnectionStrings:WebGis belum diisi.");

    [GeneratedRegex("^layer_[0-9a-f]{32}$")]
    private static partial Regex LayerTablePattern();

    public async ValueTask DisposeAsync()
    {
        if (dataSource != null) await dataSource.DisposeAsync();
    }
}

public enum TileStatus { Ok, Empty, NotFound, Invalid, TooDense, TooLarge, Timeout }

public sealed record TileResult(TileStatus Status, byte[]? Data)
{
    public static TileResult Ok(byte[] data) => new(TileStatus.Ok, data);
    public static readonly TileResult Empty = new(TileStatus.Empty, null);
    public static readonly TileResult NotFound = new(TileStatus.NotFound, null);
    public static readonly TileResult Invalid = new(TileStatus.Invalid, null);
    public static readonly TileResult TooDense = new(TileStatus.TooDense, null);
    public static readonly TileResult TooLarge = new(TileStatus.TooLarge, null);
    public static readonly TileResult Timeout = new(TileStatus.Timeout, null);
}
