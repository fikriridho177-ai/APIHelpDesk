using SkiaSharp;

namespace AuthUtility.Api.Wad;

// Foto laporan WAD: JPG/PNG/WEBP, maksimal 5 MB per file, maksimal 5 file per laporan.
public static class WadPhotoPolicy
{
    public static string Root(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configured = configuration["Wad:Uploads:Path"];
        var root = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(environment.ContentRootPath, "App_Data", "wad-photos")
            : Path.GetFullPath(configured);
        var webRoot = Path.GetFullPath(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"));
        if (Path.GetRelativePath(webRoot, root) is var relative && relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar) && !Path.IsPathRooted(relative))
            throw new InvalidOperationException("Wad:Uploads:Path harus berada di luar wwwroot.");
        return root;
    }

    public static string? Validate(string name, string mime, long length, ReadOnlySpan<byte> bytes)
    {
        if (length <= 0 || length > WadReportRules.MaxPhotoBytes) return "Foto harus berisi data dan maksimal 5 MB.";
        var extension = Path.GetExtension(name).ToLowerInvariant();
        var valid = extension switch
        {
            ".jpg" or ".jpeg" => mime == "image/jpeg" && bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255,
            ".png" => mime == "image/png" && bytes.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            ".webp" => mime == "image/webp" && bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8),
            _ => false
        };
        return valid ? null : "Foto harus JPG, PNG, atau WEBP dengan isi berkas yang sesuai.";
    }

    public static string? ValidateImage(Stream stream)
    {
        using var codec = SKCodec.Create(stream);
        if (codec == null) return "Isi foto rusak atau tidak dapat dibaca.";
        if (codec.Info.Width <= 0 || codec.Info.Height <= 0 || (long)codec.Info.Width * codec.Info.Height > 40_000_000)
            return "Resolusi foto maksimal 40 megapiksel.";
        if (codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png or SKEncodedImageFormat.Webp))
            return "Format isi foto tidak didukung.";
        using var bitmap = new SKBitmap(new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        return codec.GetPixels(bitmap.Info, bitmap.GetPixels()) == SKCodecResult.Success
            ? null : "Isi foto tidak lengkap atau rusak.";
    }

    public static string RelativePath(string module, long reportId, string extension)
    {
        var date = DateTime.UtcNow.AddHours(7);
        return $"{module.ToLowerInvariant()}/{date:yyyy}/{date:MM}/{reportId}/{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
    }

    public static string? ResolvePath(string root, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath)) return null;
        var fullRoot = Path.GetFullPath(root);
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
        return fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? fullPath : null;
    }

    public static string? ContentType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => null
    };
}
