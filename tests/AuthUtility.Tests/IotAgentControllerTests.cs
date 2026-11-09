using System.Text.Json;
using AuthUtility.Api.Controllers;
using AuthUtility.Api.IoT;
using AuthUtility.Application.DTOs.IoT;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AuthUtility.Tests;

public sealed class IotAgentControllerTests
{
    [Fact]
    public async Task Heartbeat_updates_last_seen_and_rejects_a_mismatched_body_identity()
    {
        await using var db = CreateDb();
        var (device, material) = await SeedDeviceAsync(db, "esp8266-heartbeat");
        var controller = CreateController(db);
        Authenticate(controller, material.PlaintextKey);

        var response = await controller.Heartbeat(new IotDeviceHeartbeatRequest
        {
            DeviceId = device.DeviceId,
            DeviceType = device.DeviceType,
            Platform = device.Platform,
            BoardType = device.BoardType,
            DeviceTimestamp = DateTimeOffset.UtcNow,
            FirmwareVersion = "1.2.3",
            IpAddress = "10.0.0.10",
            Rssi = -61,
            UptimeSeconds = 123
        });

        Assert.IsType<OkObjectResult>(response);
        var saved = await db.IotDeviceAgents.SingleAsync(x => x.Id == device.Id);
        Assert.Equal(IotDeviceStatus.Online, saved.Status);
        Assert.Equal("1.2.3", saved.LastFirmwareVersion);
        Assert.Equal("10.0.0.10", saved.LastIpAddress);
        Assert.Single(await db.IotDeviceReports.ToListAsync());

        var mismatch = await controller.Heartbeat(new IotDeviceHeartbeatRequest
        {
            DeviceId = "another-device",
            DeviceTimestamp = DateTimeOffset.UtcNow,
            FirmwareVersion = "1.2.3"
        });

        Assert.IsType<ConflictObjectResult>(mismatch);
    }

    [Fact]
    public async Task Heartbeat_confirms_a_rebooting_job_when_the_target_version_is_reported()
    {
        await using var db = CreateDb();
        var (device, material) = await SeedDeviceAsync(db, "esp8266-reboot-confirmation");
        var package = new IotFirmwarePackage
        {
            Id = Guid.NewGuid(),
            PackageName = "ESP8266 agent",
            Version = "2.0.0",
            TargetMcu = "ESP8266",
            CompatibleBoardProfilesJson = "[\"esp8266-node\"]",
            CompatibleHardwareRevisionsJson = "[]",
            FileName = "agent.bin",
            StoragePath = "agent.bin",
            SizeBytes = 1,
            Sha256 = new string('a', 64)
        };
        db.IotFirmwarePackages.Add(package);
        db.IotOtaJobs.Add(new IotOtaJob
        {
            Id = Guid.NewGuid(),
            DeviceAgentId = device.Id,
            FirmwarePackageId = package.Id,
            FirmwarePackage = package,
            Status = IotOtaStatus.Rebooting,
            Progress = 100
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        Authenticate(controller, material.PlaintextKey);
        var response = await controller.Heartbeat(new IotDeviceHeartbeatRequest
        {
            DeviceId = device.DeviceId,
            DeviceTimestamp = DateTimeOffset.UtcNow,
            FirmwareVersion = package.Version
        });

        Assert.IsType<OkObjectResult>(response);
        var job = await db.IotOtaJobs.Include(x => x.Events).SingleAsync();
        Assert.Equal(IotOtaStatus.Succeeded, job.Status);
        Assert.Equal(package.Version, job.ReportedFirmwareVersion);
        Assert.Contains(job.Events, x => x.EventType == IotOtaEventType.Confirmed);
    }

    [Fact]
    public async Task Heartbeat_rejects_a_mismatched_hardware_profile()
    {
        await using var db = CreateDb();
        var (device, material) = await SeedDeviceAsync(db, "hardware-profile-mismatch");
        var controller = CreateController(db);
        Authenticate(controller, material.PlaintextKey);

        var response = await controller.Heartbeat(new IotDeviceHeartbeatRequest
        {
            DeviceId = device.DeviceId,
            DeviceType = "DATALOGGER",
            Platform = "ESP32",
            BoardType = "DATALOGGER_ESP32_SENSOR_V1",
            DeviceTimestamp = DateTimeOffset.UtcNow,
            FirmwareVersion = "1.1.3"
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(response);
        Assert.Contains("deviceType", JsonSerializer.Serialize(badRequest.Value));
    }

    [Fact]
    public async Task Telemetry_is_idempotent_and_rejects_oversized_or_non_numeric_readings()
    {
        await using var db = CreateDb();
        var (device, material) = await SeedDeviceAsync(db, "esp8266-telemetry");
        var controller = CreateController(db, new IotDeviceOptions { MaxTelemetryBytes = 256 });
        Authenticate(controller, material.PlaintextKey);
        var timestamp = DateTimeOffset.UtcNow;
        var readings = new Dictionary<string, JsonElement>
        {
            ["temperature"] = JsonDocument.Parse("27.4").RootElement.Clone()
        };

        var first = await controller.Telemetry(new IotTelemetryRequest
        {
            DeviceId = device.DeviceId,
            DeviceTimestamp = timestamp,
            FirmwareVersion = "1.2.3",
            Readings = readings
        });
        var duplicate = await controller.Telemetry(new IotTelemetryRequest
        {
            DeviceId = device.DeviceId,
            DeviceTimestamp = timestamp,
            FirmwareVersion = "1.2.3",
            Readings = readings
        });

        Assert.IsType<OkObjectResult>(first);
        Assert.IsType<OkObjectResult>(duplicate);
        Assert.Single(await db.IotDeviceReports.Where(x => x.Kind == "telemetry").ToListAsync());

        var invalid = await controller.Telemetry(new IotTelemetryRequest
        {
            DeviceId = device.DeviceId,
            DeviceTimestamp = DateTimeOffset.UtcNow,
            FirmwareVersion = "1.2.3",
            Readings = new Dictionary<string, JsonElement>
            {
                ["temperature"] = JsonDocument.Parse("\"not-a-number\"").RootElement.Clone()
            }
        });
        Assert.IsType<BadRequestObjectResult>(invalid);

        var oversized = await controller.Telemetry(new IotTelemetryRequest
        {
            DeviceId = device.DeviceId,
            DeviceTimestamp = DateTimeOffset.UtcNow,
            FirmwareVersion = "1.2.3",
            Readings = Enumerable.Range(0, 100)
                .ToDictionary(x => $"sensor-{x}", _ => JsonDocument.Parse("1").RootElement.Clone())
        });
        Assert.IsType<BadRequestObjectResult>(oversized);
    }

    [Fact]
    public async Task Configuration_can_be_read_and_acknowledged_and_missing_key_is_unauthorized()
    {
        await using var db = CreateDb();
        var (device, material) = await SeedDeviceAsync(db, "esp8266-config");
        db.IotDeviceConfigurations.Add(new IotDeviceConfiguration
        {
            Id = Guid.NewGuid(),
            DeviceAgentId = device.Id,
            DesiredVersion = 7,
            DesiredJson = "{\"intervalSeconds\":30}"
        });
        await db.SaveChangesAsync();

        var anonymous = CreateController(db);
        var unauthorized = await anonymous.GetConfiguration();
        Assert.IsType<UnauthorizedObjectResult>(unauthorized);

        var controller = CreateController(db);
        Authenticate(controller, material.PlaintextKey);
        var configuration = await controller.GetConfiguration();
        Assert.IsType<OkObjectResult>(configuration);

        var ack = await controller.AcknowledgeConfiguration(7, new IotConfigurationAckRequest
        {
            DeviceId = device.DeviceId,
            Status = IotConfigurationStatus.Applied
        });

        Assert.IsType<OkObjectResult>(ack);
        var saved = await db.IotDeviceConfigurations.SingleAsync(x => x.DeviceAgentId == device.Id);
        Assert.Equal(IotConfigurationStatus.Applied, saved.ReportedStatus);
        Assert.Equal(7, saved.ReportedVersion);
    }

    private static IotAgentController CreateController(AppDbContext db, IotDeviceOptions? options = null)
    {
        var service = new IotDeviceCredentialService(db);
        var controller = new IotAgentController(
            db,
            service,
            Options.Create(options ?? new IotDeviceOptions()),
            NullLogger<IotAgentController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        return controller;
    }

    private static void Authenticate(IotAgentController controller, string key)
    {
        controller.Request.Headers["X-Device-Key"] = key;
    }

    private static async Task<(IotDeviceAgent Device, IotDeviceCredentialMaterial Material)> SeedDeviceAsync(
        AppDbContext db,
        string deviceId)
    {
        var device = new IotDeviceAgent
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            Name = deviceId,
            Platform = "ESP8266",
            BoardType = "NodeMCU"
        };
        var service = new IotDeviceCredentialService(db);
        var material = service.Create();
        db.IotDeviceAgents.Add(device);
        db.IotDeviceCredentials.Add(new IotDeviceCredential
        {
            Id = Guid.NewGuid(),
            DeviceAgentId = device.Id,
            KeyPrefix = material.Prefix,
            Salt = material.Salt,
            KeyHash = material.Hash
        });
        await db.SaveChangesAsync();
        return (device, material);
    }

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
