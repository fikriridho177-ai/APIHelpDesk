using AuthUtility.Domain.Entities;

namespace AuthUtility.Api.Sensus;

public static class SensusPhotoPolicy
{
    public const int MaxFiles = 5;
    public const long MaxBytes = 5 * 1024 * 1024;

    public static bool CanEdit(int userId, int ownerId, string status, bool canSubmit) =>
        userId == ownerId && canSubmit && status is SnsStatus.Submitted or SnsStatus.Revisi;

    public static string? Validate(string name, string mime, long length, ReadOnlySpan<byte> bytes)
    {
        if (length <= 0 || length > MaxBytes) return "Foto harus berisi data dan maksimal 5 MB.";
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

    public static string Root(IWebHostEnvironment environment) =>
        Path.Combine(environment.ContentRootPath, "wwwroot", "uploads", "sensus");

    public static string? OwnedPath(string root, long id, string url)
    {
        var parts = url.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 6 || parts[0] != "uploads" || parts[1] != "sensus" || parts[4] != id.ToString()
            || parts[2].Length != 4 || !int.TryParse(parts[2], out _)
            || !int.TryParse(parts[3], out var month) || month is < 1 or > 12
            || !Guid.TryParseExact(Path.GetFileNameWithoutExtension(parts[5]), "N", out _)
            || !new[] { ".jpg", ".jpeg", ".png", ".webp" }.Contains(Path.GetExtension(parts[5]).ToLowerInvariant())) return null;
        var path = Path.GetFullPath(Path.Combine(root, parts[2], parts[3], parts[4], parts[5]));
        return path.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? path : null;
    }
}
