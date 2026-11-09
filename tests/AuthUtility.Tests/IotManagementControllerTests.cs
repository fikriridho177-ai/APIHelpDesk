using System.Security.Claims;
using System.Text.Json;
using AuthUtility.Api.Controllers;
using AuthUtility.Api.IoT;
using AuthUtility.Application.DTOs.IoT;
using AuthUtility.Application.DTOs.Security;
using AuthUtility.Application.Interfaces;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AuthUtility.Tests;

public sealed class IotManagementControllerTests
{
    [Fact]
    public async Task Management_requires_permission_and_provisioning_returns_key_once()
    {
        await using var db = CreateDb();
        var denied = CreateManagementController(db, allow: false);
        var forbidden = await denied.ListDevices();
        Assert.IsType<ForbidResult>(forbidden);

        var controller = CreateManagementController(db, allow: true);
        var result = await controller.ProvisionDevice(new IotDeviceProvisionRequest
        {
            DeviceId = "esp8266-admin-001",
            Name = "Admin device",
            Platform = "ESP8266",
            BoardType = "esp8266-node"
        });

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var response = Assert.IsType<IotDeviceProvisionResponse>(created.Value);
        Assert.StartsWith("iot_", response.DeviceKey, StringComparison.Ordinal);
        Assert.NotNull(await db.IotDeviceCredentials.SingleAsync());
        Assert.DoesNotContain(response.DeviceKey, await db.IotDeviceCredentials
            .Select(x => x.KeyHash.ToString())
            .ToListAsync());
    }

    [Fact]
    public async Task Provisioning_derives_the_hardware_identity_from_an_esp32_board_profile()
    {
        await using var db = CreateDb();
        var controller = CreateManagementController(db, allow: true);

        var result = await controller.ProvisionDevice(new IotDeviceProvisionRequest
        {
            DeviceId = "datalogger-esp32-001",
            Name = "DataLogger ESP32",
            DeviceType = "ESP8266",
            Platform = "ESP8266",
            BoardType = "esp32-devkit-v1"
        });

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var response = Assert.IsType<IotDeviceProvisionResponse>(created.Value);
        Assert.Equal("DATALOGGER", response.Device.DeviceType);
        Assert.Equal("ESP32", response.Device.Platform);
        Assert.Equal("esp32-devkit-v1", response.Device.BoardType);
    }

    [Fact]
    public async Task Ota_assignment_rejects_incompatible_targets_and_creates_queued_job_for_compatible_device()
    {
        await using var db = CreateDb();
        var controller = CreateManagementController(db, allow: true);
        var compatible = new IotDeviceAgent
        {
            Id = Guid.NewGuid(),
            DeviceId = "esp8266-compatible",
            Name = "Compatible",
            BoardType = "esp8266-node",
            IsActive = true
        };
        var incompatible = new IotDeviceAgent
        {
            Id = Guid.NewGuid(),
            DeviceId = "mega-incompatible",
            Name = "Mega",
            BoardType = "mega2560-esp8266",
            IsActive = true
        };
        var firmware = new IotFirmwarePackage
        {
            Id = Guid.NewGuid(),
            PackageName = "ESP8266",
            Version = "2.0.0",
            TargetMcu = "ESP8266",
            CompatibleBoardProfilesJson = "[\"esp8266-node\"]",
            CompatibleHardwareRevisionsJson = "[]",
            FileName = "firmware.bin",
            StoragePath = "firmware.bin",
            SizeBytes = 10,
            Sha256 = "hash"
        };
        db.IotDeviceAgents.AddRange(compatible, incompatible);
        db.IotFirmwarePackages.Add(firmware);
        await db.SaveChangesAsync();

        var rejected = await controller.CreateOta(new IotOtaCreateRequest
        {
            FirmwareId = firmware.Id,
            DeviceIds = new[] { incompatible.DeviceId }
        });
        Assert.IsType<BadRequestObjectResult>(rejected);

        var accepted = await controller.CreateOta(new IotOtaCreateRequest
        {
            FirmwareId = firmware.Id,
            DeviceIds = new[] { compatible.DeviceId }
        });
        Assert.IsType<OkObjectResult>(accepted);
        var job = await db.IotOtaJobs.SingleAsync();
        Assert.Equal(IotOtaStatus.Queued, job.Status);
        Assert.Equal(compatible.Id, job.DeviceAgentId);
    }

    [Fact]
    public async Task Device_scoped_ota_events_require_order_and_exact_post_reboot_version()
    {
        await using var db = CreateDb();
        var credentials = new IotDeviceCredentialService(db);
        var device = new IotDeviceAgent
        {
            Id = Guid.NewGuid(),
            DeviceId = "esp8266-ota",
            Name = "OTA device",
            BoardType = "esp8266-node",
            IsActive = true
        };
        var material = credentials.Create();
        db.IotDeviceAgents.Add(device);
        db.IotDeviceCredentials.Add(new IotDeviceCredential
        {
            Id = Guid.NewGuid(),
            DeviceAgentId = device.Id,
            KeyPrefix = material.Prefix,
            Salt = material.Salt,
            KeyHash = material.Hash
        });
        var firmware = new IotFirmwarePackage
        {
            Id = Guid.NewGuid(),
            PackageName = "ESP8266",
            Version = "2.0.0",
            TargetMcu = "ESP8266",
            CompatibleBoardProfilesJson = "[\"esp8266-node\"]",
            CompatibleHardwareRevisionsJson = "[]",
            FileName = "firmware.bin",
            StoragePath = "firmware.bin",
            SizeBytes = 10,
            Sha256 = "hash"
        };
        var job = new IotOtaJob
        {
            Id = Guid.NewGuid(),
            DeviceAgentId = device.Id,
            FirmwarePackageId = firmware.Id,
            FirmwarePackage = firmware
        };
        db.IotFirmwarePackages.Add(firmware);
        db.IotOtaJobs.Add(job);
        await db.SaveChangesAsync();

        var agent = new IotAgentController(
            db,
            credentials,
            Options.Create(new IotDeviceOptions()),
            NullLogger<IotAgentController>.Instance);
        agent.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        agent.Request.Headers["X-Device-Key"] = material.PlaintextKey;

        var skipped = await agent.RecordOtaEvent(job.Id, new IotOtaEventRequest
        {
            DeviceId = device.DeviceId,
            EventType = IotOtaEventType.Installing,
            Progress = 75,
            OccurredAt = DateTimeOffset.UtcNow
        });
        Assert.IsType<ConflictObjectResult>(skipped);

        foreach (var (eventType, progress) in new[]
        {
            (IotOtaEventType.Downloading, 0),
            (IotOtaEventType.Verifying, 45),
            (IotOtaEventType.Installing, 75),
            (IotOtaEventType.Rebooting, 100),
            (IotOtaEventType.WaitingConfirmation, 100)
        })
        {
            var response = await agent.RecordOtaEvent(job.Id, new IotOtaEventRequest
            {
                DeviceId = device.DeviceId,
                EventType = eventType,
                Progress = progress,
                OccurredAt = DateTimeOffset.UtcNow
            });
            Assert.IsType<OkObjectResult>(response);
        }

        var mismatch = await agent.ConfirmOta(job.Id, new IotOtaConfirmRequest
        {
            DeviceId = device.DeviceId,
            ReportedFirmwareVersion = "1.9.9",
            ReportedAt = DateTimeOffset.UtcNow
        });
        Assert.IsType<ConflictObjectResult>(mismatch);
        Assert.Equal(IotOtaStatus.Failed, await db.IotOtaJobs.Select(x => x.Status).SingleAsync());

        var successJob = new IotOtaJob
        {
            Id = Guid.NewGuid(),
            DeviceAgentId = device.Id,
            FirmwarePackageId = firmware.Id,
            Status = IotOtaStatus.WaitingConfirmation,
            Progress = 100,
            FirmwarePackage = firmware
        };
        db.IotOtaJobs.Add(successJob);
        await db.SaveChangesAsync();

        var success = await agent.ConfirmOta(successJob.Id, new IotOtaConfirmRequest
        {
            DeviceId = device.DeviceId,
            ReportedFirmwareVersion = "2.0.0",
            ReportedAt = DateTimeOffset.UtcNow
        });
        Assert.IsType<OkObjectResult>(success);
        Assert.Equal(IotOtaStatus.Succeeded, await db.IotOtaJobs
            .Where(x => x.Id == successJob.Id)
            .Select(x => x.Status)
            .SingleAsync());

        var repeatedSuccess = await agent.ConfirmOta(successJob.Id, new IotOtaConfirmRequest
        {
            DeviceId = device.DeviceId,
            ReportedFirmwareVersion = "2.0.0",
            ReportedAt = DateTimeOffset.UtcNow
        });
        Assert.IsType<OkObjectResult>(repeatedSuccess);
    }

    [Fact]
    public async Task Firmware_download_is_limited_to_the_assigned_device_job()
    {
        var root = Path.Combine(Path.GetTempPath(), $"apihelpdesk-download-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            await using var db = CreateDb();
            var credentials = new IotDeviceCredentialService(db);
            var first = new IotDeviceAgent
            {
                Id = Guid.NewGuid(),
                DeviceId = "esp8266-download-1",
                Name = "Download 1",
                BoardType = "esp8266-node",
                IsActive = true
            };
            var second = new IotDeviceAgent
            {
                Id = Guid.NewGuid(),
                DeviceId = "esp8266-download-2",
                Name = "Download 2",
                BoardType = "esp8266-node",
                IsActive = true
            };
            var firstMaterial = credentials.Create();
            var secondMaterial = credentials.Create();
            db.IotDeviceAgents.AddRange(first, second);
            db.IotDeviceCredentials.AddRange(
                new IotDeviceCredential
                {
                    Id = Guid.NewGuid(), DeviceAgentId = first.Id, KeyPrefix = firstMaterial.Prefix,
                    Salt = firstMaterial.Salt, KeyHash = firstMaterial.Hash
                },
                new IotDeviceCredential
                {
                    Id = Guid.NewGuid(), DeviceAgentId = second.Id, KeyPrefix = secondMaterial.Prefix,
                    Salt = secondMaterial.Salt, KeyHash = secondMaterial.Hash
                });
            var package = new IotFirmwarePackage
            {
                Id = Guid.NewGuid(),
                PackageName = "ESP8266",
                Version = "3.0.0",
                TargetMcu = "ESP8266",
                CompatibleBoardProfilesJson = "[\"esp8266-node\"]",
                CompatibleHardwareRevisionsJson = "[]",
                FileName = "firmware.bin",
                StoragePath = "stored.bin",
                SizeBytes = 3,
                Sha256 = "hash"
            };
            db.IotFirmwarePackages.Add(package);
            db.IotOtaJobs.Add(new IotOtaJob
            {
                Id = Guid.NewGuid(),
                DeviceAgentId = second.Id,
                FirmwarePackageId = package.Id,
                FirmwarePackage = package
            });
            await db.SaveChangesAsync();
            await File.WriteAllBytesAsync(Path.Combine(root, package.StoragePath), new byte[] { 1, 2, 3 });

            var service = new IotFirmwareService(db, new IotFirmwareOptions { StoragePath = root });
            var agent = new IotAgentController(db, credentials, Options.Create(new IotDeviceOptions()), NullLogger<IotAgentController>.Instance, service);
            agent.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
            agent.Request.Headers["X-Device-Key"] = firstMaterial.PlaintextKey;

            var denied = await agent.DownloadFirmware(package.Id);
            Assert.IsType<ForbidResult>(denied);

            db.IotOtaJobs.Add(new IotOtaJob
            {
                Id = Guid.NewGuid(),
                DeviceAgentId = first.Id,
                FirmwarePackageId = package.Id,
                FirmwarePackage = package
            });
            await db.SaveChangesAsync();
            var allowed = await agent.DownloadFirmware(package.Id);
            Assert.IsType<PhysicalFileResult>(allowed);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static IotManagementController CreateManagementController(AppDbContext db, bool allow)
    {
        var credentials = new IotDeviceCredentialService(db);
        var firmware = new IotFirmwareService(db, new IotFirmwareOptions
        {
            StoragePath = Path.Combine(Path.GetTempPath(), $"apihelpdesk-management-{Guid.NewGuid():N}")
        });
        var controller = new IotManagementController(
            db,
            new FakePermissionService(allow),
            credentials,
            firmware,
            NullLogger<IotManagementController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity("test"))
            }
        };
        return controller;
    }

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private sealed class FakePermissionService(bool allow) : IPermissionService
    {
        public Task<bool> IsSuperAdminAsync(ClaimsPrincipal principal) => Task.FromResult(false);

        public Task<bool> HasPermissionAsync(ClaimsPrincipal principal, string menuCode, MenuPermission permission) =>
            Task.FromResult(allow);

        public Task<CurrentMenuPermissionDto?> GetPermissionsAsync(ClaimsPrincipal principal, string menuCode) =>
            Task.FromResult<CurrentMenuPermissionDto?>(null);
    }
}
