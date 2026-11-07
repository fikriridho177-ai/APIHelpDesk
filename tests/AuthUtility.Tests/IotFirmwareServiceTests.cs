using System.Text;
using AuthUtility.Api.IoT;
using AuthUtility.Application.DTOs.IoT;
using AuthUtility.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Tests;

public sealed class IotFirmwareServiceTests
{
    [Fact]
    public async Task Stores_bin_with_server_hash_and_generated_safe_path()
    {
        var root = CreateTempDirectory();
        try
        {
            await using var db = CreateDb();
            var service = new IotFirmwareService(db, new IotFirmwareOptions
            {
                StoragePath = root,
                MaxBytes = 1024
            });
            var bytes = Encoding.UTF8.GetBytes("firmware-binary");
            await using var stream = new MemoryStream(bytes);
            var file = new FormFile(stream, 0, bytes.Length, "file", "..\\trusted.bin")
            {
                Headers = new HeaderDictionary(),
                ContentType = "application/octet-stream"
            };

            var package = await service.StoreAsync(file, new IotFirmwareUploadMetadata
            {
                PackageName = "ESP8266 production",
                Version = "1.0.1",
                TargetMcu = "ESP8266",
                CompatibleBoardProfiles = new[] { "esp8266-node" }
            });

            Assert.Equal(bytes.Length, package.SizeBytes);
            Assert.Equal("ESP8266", package.TargetMcu);
            Assert.Equal("4924584E5B634794B60D87F7CFC6ED84086489968F9CFDE652C095D41D063EE0", package.Sha256);
            Assert.DoesNotContain("..", package.StoragePath, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(root, package.StoragePath)));
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task Rejects_oversized_non_bin_and_non_esp8266_packages()
    {
        var root = CreateTempDirectory();
        try
        {
            await using var db = CreateDb();
            var service = new IotFirmwareService(db, new IotFirmwareOptions
            {
                StoragePath = root,
                MaxBytes = 4
            });
            var bytes = Encoding.UTF8.GetBytes("too-large");
            await using var stream = new MemoryStream(bytes);
            var oversized = new FormFile(stream, 0, bytes.Length, "file", "firmware.bin");

            await Assert.ThrowsAsync<IotFirmwareValidationException>(() =>
                service.StoreAsync(oversized, new IotFirmwareUploadMetadata
                {
                    PackageName = "Too large",
                    Version = "1.0.0",
                    TargetMcu = "ESP8266"
                }));

            await using var secondStream = new MemoryStream(new byte[] { 1, 2 });
            var wrongExtension = new FormFile(secondStream, 0, 2, "file", "firmware.txt");
            await Assert.ThrowsAsync<IotFirmwareValidationException>(() =>
                service.StoreAsync(wrongExtension, new IotFirmwareUploadMetadata
                {
                    PackageName = "Wrong extension",
                    Version = "1.0.0",
                    TargetMcu = "ESP32"
                }));
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static string CreateTempDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"apihelpdesk-iot-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteTempDirectory(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}
