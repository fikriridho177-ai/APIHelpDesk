using AuthUtility.Api.WebGis;
using AuthUtility.Application.Interfaces;
using AuthUtility.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace AuthUtility.Api.Controllers;

// Peta GIS (menu GIS_PETA): viewer WebGIS_PT_RSUP di dalam HelpDesk.
// Data dibaca read-only dari database PostGIS WebGIS lewat WebGisReader. Hak: Read di menu GIS_PETA.
[ApiController]
[Route("api/gis")]
[Authorize]
public class GisController(
    WebGisReader webGis,
    IPermissionService permissions,
    ILogger<GisController> logger
) : ControllerBase
{
    public const string MenuCode = "GIS_PETA";

    private Task<bool> CanRead() => permissions.HasPermissionAsync(User, MenuCode, MenuPermission.Read);

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        if (!await CanRead()) return Forbid();
        var (connected, message) = await webGis.CheckAsync(ct);
        if (!connected) logger.LogWarning("Peta GIS belum terhubung: {Message}", message);
        return Ok(new { configured = webGis.Configured, connected, rasterAvailable = webGis.RasterAvailable, message });
    }

    [HttpGet("layers")]
    public async Task<IActionResult> GetLayers(CancellationToken ct)
    {
        if (!await CanRead()) return Forbid();
        if (!webGis.Configured) return Unavailable("Peta GIS belum terhubung ke database WebGIS.");
        NoStore();
        try
        {
            return Ok(new { layers = await webGis.ListLayersAsync(ct) });
        }
        catch (Exception error) when (error is NpgsqlException or PostgresException)
        {
            logger.LogError(error, "Gagal membaca katalog layer WebGIS");
            return Unavailable("Data peta sementara tidak tersedia. Silakan coba lagi.");
        }
    }

    [HttpGet("layers/{id:guid}/tiles/{z:int}/{x:int}/{y}")]
    public async Task<IActionResult> GetVectorTile(Guid id, int z, int x, string y, CancellationToken ct)
    {
        if (!await CanRead()) return Forbid();
        if (!webGis.Configured) return Unavailable("Peta GIS belum terhubung ke database WebGIS.");
        if (!TryTile(z, x, y, ".pbf", out var row)) return BadRequest(new { code = "INVALID_TILE", message = "Koordinat tile tidak valid." });
        NoStore();
        try
        {
            return ToResponse(await webGis.GetVectorTileAsync(id, z, x, row, ct), "application/vnd.mapbox-vector-tile");
        }
        catch (Exception error) when (error is NpgsqlException or PostgresException)
        {
            logger.LogError(error, "Gagal membaca tile vector WebGIS {Layer} {Z}/{X}/{Y}", id, z, x, row);
            return Unavailable("Data peta sementara tidak tersedia. Silakan coba lagi.");
        }
    }

    [HttpGet("layers/{id:guid}/raster/{z:int}/{x:int}/{y}")]
    public async Task<IActionResult> GetRasterTile(Guid id, int z, int x, string y, CancellationToken ct)
    {
        if (!await CanRead()) return Forbid();
        if (!webGis.Configured) return Unavailable("Peta GIS belum terhubung ke database WebGIS.");
        if (!TryTile(z, x, y, ".png", out var row)) return BadRequest(new { code = "INVALID_TILE", message = "Koordinat tile tidak valid." });
        NoStore();
        try
        {
            return ToResponse(await webGis.GetRasterTileAsync(id, z, x, row, ct), "image/png");
        }
        catch (Exception error) when (error is NpgsqlException or PostgresException or IOException)
        {
            logger.LogError(error, "Gagal membaca tile raster WebGIS {Layer} {Z}/{X}/{Y}", id, z, x, row);
            return Unavailable("Data peta sementara tidak tersedia. Silakan coba lagi.");
        }
    }

    // Validasi sama dengan WebGIS: z 0..22, x/y < 2^z.
    private static bool TryTile(int z, int x, string yText, string extension, out int y)
    {
        y = -1;
        var text = yText.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ? yText[..^extension.Length] : yText;
        if (!int.TryParse(text, out var value) || z < 0 || z > 22 || x < 0 || value < 0) return false;
        var size = 1L << z;
        if (x >= size || value >= size) return false;
        y = value;
        return true;
    }

    private IActionResult ToResponse(TileResult result, string contentType) => result.Status switch
    {
        TileStatus.Ok => File(result.Data!, contentType),
        TileStatus.Empty => File(Array.Empty<byte>(), contentType),
        TileStatus.Invalid => BadRequest(new { code = "INVALID_TILE", message = "Koordinat tile tidak valid." }),
        TileStatus.TooDense or TileStatus.TooLarge => UnprocessableEntity(new
        {
            code = result.Status == TileStatus.TooDense ? "TILE_TOO_DENSE" : "TILE_TOO_LARGE",
            message = "Tile ini melebihi batas pengiriman peta. Perbesar peta atau minta administrator GIS memecah dataset.",
        }),
        TileStatus.Timeout => Unavailable("Tile terlalu lama diproses. Perbesar peta lalu coba lagi."),
        _ => NotFound(new { code = "LAYER_NOT_FOUND", message = "Layer tidak tersedia." }),
    };

    private ObjectResult Unavailable(string message) =>
        StatusCode(StatusCodes.Status503ServiceUnavailable, new { code = "GIS_UNAVAILABLE", message });

    private void NoStore() => Response.Headers.CacheControl = "private, no-store";
}
