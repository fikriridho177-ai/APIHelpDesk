using System.Security.Cryptography;
using System.Text.Json;
using AuthUtility.Api.IoT;
using AuthUtility.Application.DTOs.IoT;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AuthUtility.Api.IoT;

public sealed class IotFirmwareOptions
{
    public string StoragePath { get; set; } = Path.Combine(AppContext.BaseDirectory, "firmware");

    public long MaxBytes { get; set; } = 50 * 1024 * 1024;
}

public sealed class IotFirmwareValidationException(string message) : Exception(message);

public sealed class IotFirmwareService
{
    private readonly AppDbContext db;
    private readonly IotFirmwareOptions options;

    public IotFirmwareService(AppDbContext db)
        : this(db, new IotFirmwareOptions())
    {
    }

    public IotFirmwareService(AppDbContext db, IotFirmwareOptions options)
    {
        this.db = db;
        this.options = options;
    }

    public IotFirmwareService(AppDbContext db, IOptions<IotFirmwareOptions> options)
        : this(db, options.Value)
    {
    }

    public async Task<IotFirmwarePackage> StoreAsync(
        IFormFile file,
        IotFirmwareUploadMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        ValidateMetadata(file, metadata);

        var root = GetStorageRoot();
        Directory.CreateDirectory(root);
        var storedFileName = $"{Guid.NewGuid():N}.bin";
        var storedPath = Path.Combine(root, storedFileName);
        long totalBytes = 0;

        try
        {
            await using var input = file.OpenReadStream();
            await using var output = new FileStream(
                storedPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                useAsync: true);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[64 * 1024];

            while (true)
            {
                var read = await input.ReadAsync(buffer.AsMemory(), cancellationToken);
                if (read == 0)
                    break;

                totalBytes += read;
                if (totalBytes > options.MaxBytes)
                    throw new IotFirmwareValidationException("Ukuran firmware melebihi batas maksimum.");

                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                hash.AppendData(buffer, 0, read);
            }

            var package = new IotFirmwarePackage
            {
                Id = Guid.NewGuid(),
                PackageName = metadata.PackageName.Trim(),
                Version = metadata.Version.Trim(),
                TargetMcu = metadata.TargetMcu.Trim().ToUpperInvariant(),
                CompatibleBoardProfilesJson = JsonSerializer.Serialize(Normalize(metadata.CompatibleBoardProfiles)),
                CompatibleHardwareRevisionsJson = JsonSerializer.Serialize(Normalize(metadata.CompatibleHardwareRevisions)),
                Notes = string.IsNullOrWhiteSpace(metadata.Notes) ? null : metadata.Notes.Trim(),
                FileName = Path.GetFileName(file.FileName),
                StoragePath = storedFileName,
                SizeBytes = totalBytes,
                Sha256 = Convert.ToHexString(hash.GetHashAndReset()),
                SignatureStatus = "unsigned",
                CreatedAt = DateTimeOffset.UtcNow
            };

            db.IotFirmwarePackages.Add(package);
            await db.SaveChangesAsync(cancellationToken);
            return package;
        }
        catch
        {
            if (File.Exists(storedPath))
                File.Delete(storedPath);
            throw;
        }
    }

    public string GetSafeFilePath(IotFirmwarePackage package)
    {
        var root = GetStorageRoot();
        var relativePath = package.StoragePath?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(relativePath)
            || Path.IsPathRooted(relativePath)
            || Path.GetFileName(relativePath) != relativePath)
        {
            throw new IotFirmwareValidationException("Path firmware tidak valid.");
        }

        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            throw new IotFirmwareValidationException("Path firmware berada di luar storage.");

        return fullPath;
    }

    public FileStream OpenRead(IotFirmwarePackage package)
    {
        var path = GetSafeFilePath(package);
        if (!File.Exists(path))
            throw new FileNotFoundException("File firmware tidak ditemukan.", path);

        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, useAsync: true);
    }

    private void ValidateMetadata(IFormFile file, IotFirmwareUploadMetadata metadata)
    {
        if (file is null || file.Length <= 0)
            throw new IotFirmwareValidationException("File firmware wajib diisi.");
        if (options.MaxBytes <= 0 || file.Length > options.MaxBytes)
            throw new IotFirmwareValidationException("Ukuran firmware melebihi batas maksimum.");
        if (!string.Equals(Path.GetExtension(file.FileName), ".bin", StringComparison.OrdinalIgnoreCase))
            throw new IotFirmwareValidationException("Firmware harus berupa file .bin.");
        if (string.IsNullOrWhiteSpace(metadata.PackageName) || string.IsNullOrWhiteSpace(metadata.Version))
            throw new IotFirmwareValidationException("Nama package dan versi firmware wajib diisi.");
        if (!IotOtaRules.IsSupportedTargetMcu(metadata.TargetMcu))
            throw new IotFirmwareValidationException("Target MCU yang didukung pada tahap ini hanya ESP8266.");
        if (!Normalize(metadata.CompatibleBoardProfiles).Any())
            throw new IotFirmwareValidationException("Minimal satu board profile kompatibel wajib diisi.");
    }

    private string GetStorageRoot() =>
        Path.GetFullPath(string.IsNullOrWhiteSpace(options.StoragePath)
            ? Path.Combine(AppContext.BaseDirectory, "firmware")
            : options.StoragePath);

    private static string[] Normalize(IEnumerable<string>? values) =>
        (values ?? Array.Empty<string>())
            .Select(value => value?.Trim() ?? "")
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
