using System.Text.Json;
using AuthUtility.Api.IoT;
using AuthUtility.Application.DTOs.IoT;
using AuthUtility.Application.Interfaces;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/iot/management")]
public sealed class IotManagementController(
    AppDbContext db,
    IPermissionService permissions,
    IIotDeviceCredentialService credentials,
    IotFirmwareService firmware,
    ILogger<IotManagementController> logger
) : ControllerBase
{
    [HttpGet("devices")]
    public async Task<IActionResult> ListDevices()
    {
        if (!await HasAccess(MenuPermission.Read))
            return Forbid();

        var devices = await db.IotDeviceAgents
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(HttpContext.RequestAborted);
        return Ok(devices.Select(ToDeviceDto));
    }

    [HttpPost("devices")]
    public async Task<IActionResult> ProvisionDevice([FromBody] IotDeviceProvisionRequest request)
    {
        if (!await HasAccess(MenuPermission.Create))
            return Forbid();
        if (string.IsNullOrWhiteSpace(request.DeviceId) || string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "deviceId dan name wajib diisi." });

        var deviceId = request.DeviceId.Trim();
        var exists = await db.IotDeviceAgents
            .AnyAsync(x => x.DeviceId.ToLower() == deviceId.ToLower(), HttpContext.RequestAborted);
        if (exists)
            return Conflict(new { message = "deviceId sudah terdaftar." });

        var now = DateTimeOffset.UtcNow;
        var hardware = HardwareIdentityForBoard(request.BoardType);
        var device = new IotDeviceAgent
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            Name = request.Name.Trim(),
            DeviceType = hardware?.DeviceType
                ?? (string.IsNullOrWhiteSpace(request.DeviceType) ? "ESP8266" : request.DeviceType.Trim()),
            Platform = hardware?.Platform
                ?? (string.IsNullOrWhiteSpace(request.Platform) ? "ESP8266" : request.Platform.Trim()),
            BoardType = request.BoardType.Trim(),
            BoardRevision = request.BoardRevision.Trim(),
            Location = request.Location.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            // Perangkat baru harus bisa melakukan heartbeat pertama. Status
            // Disabled ditujukan untuk perangkat yang sengaja dinonaktifkan;
            // provisioning sendiri bukan tindakan disable.
            Status = IotDeviceStatus.Offline,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        var material = credentials.Create();

        db.IotDeviceAgents.Add(device);
        db.IotDeviceCredentials.Add(new IotDeviceCredential
        {
            Id = Guid.NewGuid(),
            DeviceAgentId = device.Id,
            KeyPrefix = material.Prefix,
            Salt = material.Salt,
            KeyHash = material.Hash,
            CreatedAt = now
        });
        db.IotDeviceConfigurations.Add(new IotDeviceConfiguration
        {
            Id = Guid.NewGuid(),
            DeviceAgentId = device.Id,
            DesiredVersion = 1,
            DesiredJson = "{}",
            DesiredStatus = IotConfigurationStatus.Applied,
            DesiredUpdatedAt = now
        });
        AddActivity(device.Id, "device", $"Perangkat {device.Name} diprovisioning.");
        await db.SaveChangesAsync(HttpContext.RequestAborted);

        var response = new IotDeviceProvisionResponse(ToDeviceDto(device), material.PlaintextKey);
        return CreatedAtAction(nameof(GetDevice), new { deviceId = device.DeviceId }, response);
    }

    [HttpGet("devices/{deviceId}")]
    public async Task<IActionResult> GetDevice(string deviceId)
    {
        if (!await HasAccess(MenuPermission.Read))
            return Forbid();

        var device = await FindDevice(deviceId);
        return device is null
            ? NotFound(new { message = "Perangkat tidak ditemukan." })
            : Ok(ToDeviceDto(device));
    }

    [HttpPatch("devices/{deviceId}")]
    public async Task<IActionResult> UpdateDevice(
        string deviceId,
        [FromBody] IotDeviceUpdateRequest request)
    {
        if (!await HasAccess(MenuPermission.Update))
            return Forbid();

        var device = await FindDevice(deviceId);
        if (device is null)
            return NotFound(new { message = "Perangkat tidak ditemukan." });

        if (!string.IsNullOrWhiteSpace(request.Name)) device.Name = request.Name.Trim();
        if (!string.IsNullOrWhiteSpace(request.BoardType))
        {
            device.BoardType = request.BoardType.Trim();
            var hardware = HardwareIdentityForBoard(device.BoardType);
            if (hardware is not null)
            {
                device.DeviceType = hardware.Value.DeviceType;
                device.Platform = hardware.Value.Platform;
            }
        }
        if (!string.IsNullOrWhiteSpace(request.BoardRevision)) device.BoardRevision = request.BoardRevision.Trim();
        if (!string.IsNullOrWhiteSpace(request.Location)) device.Location = request.Location.Trim();
        if (request.Description is not null) device.Description = request.Description.Trim();
        if (request.IsActive.HasValue)
        {
            device.IsActive = request.IsActive.Value;
            if (!device.IsActive)
                device.Status = IotDeviceStatus.Disabled;
        }
        device.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(HttpContext.RequestAborted);
        return Ok(ToDeviceDto(device));
    }

    [HttpDelete("devices/{deviceId}")]
    public async Task<IActionResult> DeleteDevice(string deviceId)
    {
        if (!await HasAccess(MenuPermission.Delete))
            return Forbid();

        var device = await FindDevice(deviceId);
        if (device is null)
            return NotFound(new { message = "Perangkat tidak ditemukan." });

        device.IsActive = false;
        device.Status = IotDeviceStatus.Disabled;
        device.UpdatedAt = DateTimeOffset.UtcNow;
        foreach (var credential in device.Credentials)
            credential.RevokedAt = DateTimeOffset.UtcNow;
        foreach (var job in await db.IotOtaJobs
                     .Where(x => x.DeviceAgentId == device.Id
                         && x.Status != IotOtaStatus.Succeeded
                         && x.Status != IotOtaStatus.Failed)
                     .ToListAsync(HttpContext.RequestAborted))
        {
            job.Status = IotOtaStatus.Failed;
            job.FailureReason = "Perangkat dinonaktifkan operator.";
            job.LastActivityAt = DateTimeOffset.UtcNow;
        }

        AddActivity(device.Id, "device", $"Perangkat {device.Name} dinonaktifkan.");
        await db.SaveChangesAsync(HttpContext.RequestAborted);
        return NoContent();
    }

    [HttpGet("devices/{deviceId}/reports")]
    public async Task<IActionResult> GetReports(string deviceId, [FromQuery] int take = 100)
    {
        if (!await HasAccess(MenuPermission.Read))
            return Forbid();
        var device = await FindDevice(deviceId);
        if (device is null)
            return NotFound(new { message = "Perangkat tidak ditemukan." });

        take = Math.Clamp(take, 1, 500);
        var reports = await db.IotDeviceReports
            .AsNoTracking()
            .Where(x => x.DeviceAgentId == device.Id)
            .OrderByDescending(x => x.ReceivedAt)
            .Take(take)
            .Select(x => new
            {
                x.Id,
                x.Kind,
                x.DeviceTimestamp,
                x.ReceivedAt,
                x.FirmwareVersion,
                x.PayloadJson,
                x.PayloadHash
            })
            .ToListAsync(HttpContext.RequestAborted);
        return Ok(reports);
    }

    [HttpGet("devices/{deviceId}/configuration")]
    public async Task<IActionResult> GetConfiguration(string deviceId)
    {
        if (!await HasAccess(MenuPermission.Read))
            return Forbid();
        var device = await FindDevice(deviceId);
        if (device is null)
            return NotFound(new { message = "Perangkat tidak ditemukan." });

        var configuration = await db.IotDeviceConfigurations
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.DeviceAgentId == device.Id, HttpContext.RequestAborted);
        return configuration is null
            ? NotFound(new { message = "Konfigurasi perangkat belum tersedia." })
            : Ok(ToConfigurationDto(configuration));
    }

    [HttpPut("devices/{deviceId}/configuration")]
    public async Task<IActionResult> SaveConfiguration(
        string deviceId,
        [FromBody] IotConfigurationSaveRequest request)
    {
        if (!await HasAccess(MenuPermission.Update))
            return Forbid();
        var device = await FindDevice(deviceId);
        if (device is null)
            return NotFound(new { message = "Perangkat tidak ditemukan." });

        var configuration = await db.IotDeviceConfigurations
            .SingleOrDefaultAsync(x => x.DeviceAgentId == device.Id, HttpContext.RequestAborted);
        var now = DateTimeOffset.UtcNow;
        var desiredJson = request.Configuration.ValueKind == JsonValueKind.Undefined
            ? "{}"
            : request.Configuration.GetRawText();
        if (configuration is null)
        {
            configuration = new IotDeviceConfiguration
            {
                Id = Guid.NewGuid(),
                DeviceAgentId = device.Id,
                DesiredVersion = 1,
                DesiredJson = desiredJson,
                DesiredStatus = IotConfigurationStatus.Pending,
                DesiredUpdatedAt = now
            };
            db.IotDeviceConfigurations.Add(configuration);
        }
        else
        {
            configuration.DesiredVersion++;
            configuration.DesiredJson = desiredJson;
            configuration.DesiredStatus = IotConfigurationStatus.Pending;
            configuration.DesiredUpdatedAt = now;
            configuration.RejectionReason = null;
        }

        AddActivity(device.Id, "configuration", $"Konfigurasi {device.Name} diperbarui.");
        await db.SaveChangesAsync(HttpContext.RequestAborted);
        return Ok(ToConfigurationDto(configuration));
    }

    [HttpGet("sensor-types")]
    public async Task<IActionResult> ListSensorTypes()
    {
        if (!await HasAccess(MenuPermission.Read))
            return Forbid();
        var types = await db.IotSensorTypes.AsNoTracking().OrderBy(x => x.Name).ToListAsync(HttpContext.RequestAborted);
        return Ok(types.Select(ToSensorTypeDto));
    }

    [HttpPost("sensor-types")]
    public async Task<IActionResult> CreateSensorType([FromBody] IotSensorTypeRequest request)
    {
        if (!await HasAccess(MenuPermission.Create))
            return Forbid();
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Code dan name sensor type wajib diisi." });
        var code = request.Code.Trim();
        if (await db.IotSensorTypes.AnyAsync(x => x.Code.ToLower() == code.ToLower(), HttpContext.RequestAborted))
            return Conflict(new { message = "Code sensor type sudah digunakan." });

        var sensorType = new IotSensorType
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = request.Name.Trim(),
            Model = request.Model?.Trim(),
            Protocol = request.Protocol.Trim(),
            Unit = request.Unit.Trim(),
            ParametersJson = ToJson(request.Parameters),
            PinRequirementsJson = ToJson(request.PinRequirements),
            DriverKey = request.DriverKey.Trim(),
            IsActive = request.IsActive,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.IotSensorTypes.Add(sensorType);
        await db.SaveChangesAsync(HttpContext.RequestAborted);
        return CreatedAtAction(nameof(ListSensorTypes), sensorType.Id, ToSensorTypeDto(sensorType));
    }

    [HttpPatch("sensor-types/{sensorTypeId:guid}")]
    public async Task<IActionResult> UpdateSensorType(Guid sensorTypeId, [FromBody] IotSensorTypeRequest request)
    {
        if (!await HasAccess(MenuPermission.Update))
            return Forbid();
        var sensorType = await db.IotSensorTypes.FindAsync(new object[] { sensorTypeId }, HttpContext.RequestAborted);
        if (sensorType is null)
            return NotFound(new { message = "Sensor type tidak ditemukan." });
        sensorType.Name = request.Name.Trim();
        sensorType.Model = request.Model?.Trim();
        sensorType.Protocol = request.Protocol.Trim();
        sensorType.Unit = request.Unit.Trim();
        sensorType.ParametersJson = ToJson(request.Parameters);
        sensorType.PinRequirementsJson = ToJson(request.PinRequirements);
        sensorType.DriverKey = request.DriverKey.Trim();
        sensorType.IsActive = request.IsActive;
        sensorType.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(HttpContext.RequestAborted);
        return Ok(ToSensorTypeDto(sensorType));
    }

    [HttpDelete("sensor-types/{sensorTypeId:guid}")]
    public async Task<IActionResult> DeleteSensorType(Guid sensorTypeId)
    {
        if (!await HasAccess(MenuPermission.Delete))
            return Forbid();
        var sensorType = await db.IotSensorTypes.FindAsync(new object[] { sensorTypeId }, HttpContext.RequestAborted);
        if (sensorType is null)
            return NotFound(new { message = "Sensor type tidak ditemukan." });
        if (await db.IotDeviceSensors.AnyAsync(x => x.SensorTypeId == sensorTypeId, HttpContext.RequestAborted))
            return Conflict(new { message = "Sensor type masih dipakai perangkat." });
        db.IotSensorTypes.Remove(sensorType);
        await db.SaveChangesAsync(HttpContext.RequestAborted);
        return NoContent();
    }

    [HttpGet("devices/{deviceId}/sensors")]
    public async Task<IActionResult> ListDeviceSensors(string deviceId)
    {
        if (!await HasAccess(MenuPermission.Read))
            return Forbid();
        var device = await FindDevice(deviceId);
        if (device is null)
            return NotFound(new { message = "Perangkat tidak ditemukan." });
        var sensors = await db.IotDeviceSensors
            .AsNoTracking()
            .Where(x => x.DeviceAgentId == device.Id)
            .OrderBy(x => x.Label)
            .ToListAsync(HttpContext.RequestAborted);
        return Ok(sensors.Select(ToDeviceSensorDto));
    }

    [HttpPost("devices/{deviceId}/sensors")]
    public async Task<IActionResult> CreateDeviceSensor(string deviceId, [FromBody] IotDeviceSensorRequest request)
    {
        if (!await HasAccess(MenuPermission.Create))
            return Forbid();
        var device = await FindDevice(deviceId);
        if (device is null)
            return NotFound(new { message = "Perangkat tidak ditemukan." });
        var type = await db.IotSensorTypes.FindAsync(new object[] { request.SensorTypeId }, HttpContext.RequestAborted);
        if (type is null)
            return BadRequest(new { message = "Sensor type tidak ditemukan." });
        if (string.IsNullOrWhiteSpace(request.Label))
            return BadRequest(new { message = "Label sensor wajib diisi." });

        var sensor = new IotDeviceSensor
        {
            Id = Guid.NewGuid(),
            DeviceAgentId = device.Id,
            SensorTypeId = type.Id,
            Label = request.Label.Trim(),
            Protocol = request.Protocol.Trim(),
            Unit = request.Unit.Trim(),
            PinMappingsJson = ToJson(request.PinMappings),
            ReadIntervalSeconds = Math.Max(1, request.ReadIntervalSeconds),
            CalibrationJson = request.Calibration is { } calibration && calibration.ValueKind != JsonValueKind.Undefined
                ? calibration.GetRawText()
                : null,
            IsEnabled = request.IsEnabled,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.IotDeviceSensors.Add(sensor);
        await db.SaveChangesAsync(HttpContext.RequestAborted);
        return Ok(ToDeviceSensorDto(sensor));
    }

    [HttpPatch("device-sensors/{sensorId:guid}")]
    public async Task<IActionResult> UpdateDeviceSensor(Guid sensorId, [FromBody] IotDeviceSensorRequest request)
    {
        if (!await HasAccess(MenuPermission.Update))
            return Forbid();
        var sensor = await db.IotDeviceSensors.FindAsync(new object[] { sensorId }, HttpContext.RequestAborted);
        if (sensor is null)
            return NotFound(new { message = "Pemasangan sensor tidak ditemukan." });
        sensor.Label = request.Label.Trim();
        sensor.Protocol = request.Protocol.Trim();
        sensor.Unit = request.Unit.Trim();
        sensor.PinMappingsJson = ToJson(request.PinMappings);
        sensor.ReadIntervalSeconds = Math.Max(1, request.ReadIntervalSeconds);
        sensor.CalibrationJson = request.Calibration is { } calibration && calibration.ValueKind != JsonValueKind.Undefined
            ? calibration.GetRawText()
            : null;
        sensor.IsEnabled = request.IsEnabled;
        sensor.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(HttpContext.RequestAborted);
        return Ok(ToDeviceSensorDto(sensor));
    }

    [HttpDelete("device-sensors/{sensorId:guid}")]
    public async Task<IActionResult> DeleteDeviceSensor(Guid sensorId)
    {
        if (!await HasAccess(MenuPermission.Delete))
            return Forbid();
        var sensor = await db.IotDeviceSensors.FindAsync(new object[] { sensorId }, HttpContext.RequestAborted);
        if (sensor is null)
            return NotFound(new { message = "Pemasangan sensor tidak ditemukan." });
        db.IotDeviceSensors.Remove(sensor);
        await db.SaveChangesAsync(HttpContext.RequestAborted);
        return NoContent();
    }

    [HttpGet("devices/{deviceId}/diagnostics")]
    public async Task<IActionResult> GetDiagnostics(string deviceId)
    {
        if (!await HasAccess(MenuPermission.Read))
            return Forbid();
        var device = await FindDevice(deviceId);
        if (device is null)
            return NotFound(new { message = "Perangkat tidak ditemukan." });
        var lastReport = await db.IotDeviceReports
            .AsNoTracking()
            .Where(x => x.DeviceAgentId == device.Id)
            .OrderByDescending(x => x.ReceivedAt)
            .FirstOrDefaultAsync(HttpContext.RequestAborted);
        return Ok(new
        {
            device_id = device.DeviceId,
            connection = device.Status.ToString().ToLowerInvariant(),
            lastSeen = device.LastSeenAt,
            lastValidReadingAt = lastReport?.ReceivedAt,
            consecutiveFailures = 0,
            note = device.Status == IotDeviceStatus.Online ? "Heartbeat terakhir diterima." : "Belum ada heartbeat terbaru."
        });
    }

    [HttpGet("firmware")]
    public async Task<IActionResult> ListFirmware()
    {
        if (!await HasAccess(MenuPermission.Read))
            return Forbid();
        var packages = await db.IotFirmwarePackages
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(HttpContext.RequestAborted);
        return Ok(packages.Select(ToFirmwareDto));
    }

    [HttpPost("firmware")]
    [RequestSizeLimit(100 * 1024 * 1024)]
    public async Task<IActionResult> UploadFirmware(
        [FromForm] IFormFile? file,
        [FromForm] string packageName,
        [FromForm] string version,
        [FromForm] string targetMcu,
        [FromForm] string? compatibleBoardProfiles = null,
        [FromForm] string? compatibleHardwareRevisions = null,
        [FromForm] string? notes = null)
    {
        if (!await HasAccess(MenuPermission.Create))
            return Forbid();
        try
        {
            var package = await firmware.StoreAsync(
                file!,
                new IotFirmwareUploadMetadata
                {
                    PackageName = packageName,
                    Version = version,
                    TargetMcu = targetMcu,
                    CompatibleBoardProfiles = ParseList(compatibleBoardProfiles),
                    CompatibleHardwareRevisions = ParseList(compatibleHardwareRevisions),
                    Notes = notes
                },
                HttpContext.RequestAborted);
            AddActivity(null, "firmware", $"Firmware {package.Version} diunggah.");
            await db.SaveChangesAsync(HttpContext.RequestAborted);
            return Ok(ToFirmwareDto(package));
        }
        catch (IotFirmwareValidationException ex)
        {
            logger.LogWarning("Firmware upload ditolak: {Reason}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("ota")]
    public async Task<IActionResult> CreateOta([FromBody] IotOtaCreateRequest request)
    {
        if (!await HasAccess(MenuPermission.Create))
            return Forbid();
        if (request.FirmwareId == Guid.Empty || request.DeviceIds.Count == 0)
            return BadRequest(new { message = "Firmware dan minimal satu device wajib dipilih." });

        var package = await db.IotFirmwarePackages.FindAsync(new object[] { request.FirmwareId }, HttpContext.RequestAborted);
        if (package is null)
            return NotFound(new { message = "Firmware tidak ditemukan." });
        if (!IotOtaRules.IsSupportedTargetMcu(package.TargetMcu))
            return BadRequest(new { message = "Target MCU firmware belum didukung untuk OTA." });

        var devices = new List<IotDeviceAgent>();
        foreach (var requestedId in request.DeviceIds)
        {
            var device = await FindDevice(requestedId);
            if (device is null)
                return NotFound(new { message = $"Perangkat '{requestedId}' tidak ditemukan." });
            if (!IsCompatible(package, device))
                return BadRequest(new { message = $"Firmware tidak kompatibel dengan perangkat '{device.DeviceId}'." });
            devices.Add(device);
        }

        var jobs = new List<IotOtaJob>();
        foreach (var device in devices.DistinctBy(x => x.Id))
        {
            var active = await db.IotOtaJobs.AnyAsync(x => x.DeviceAgentId == device.Id
                && x.Status != IotOtaStatus.Succeeded
                && x.Status != IotOtaStatus.Failed, HttpContext.RequestAborted);
            if (active)
                return Conflict(new { message = $"Perangkat '{device.DeviceId}' sudah memiliki job OTA aktif." });

            var now = DateTimeOffset.UtcNow;
            var job = new IotOtaJob
            {
                Id = Guid.NewGuid(),
                DeviceAgentId = device.Id,
                FirmwarePackageId = package.Id,
                Status = IotOtaStatus.Queued,
                Progress = 0,
                CurrentFirmwareVersion = device.LastFirmwareVersion,
                RequestedAt = now,
                LastActivityAt = now
            };
            db.IotOtaJobs.Add(job);
            AddActivity(device.Id, "ota", $"OTA firmware {package.Version} dijadwalkan.");
            jobs.Add(job);
        }

        await db.SaveChangesAsync(HttpContext.RequestAborted);
        return Ok(jobs.Select(ToOtaJobDto));
    }

    [HttpGet("ota")]
    public async Task<IActionResult> ListOta()
    {
        if (!await HasAccess(MenuPermission.Read))
            return Forbid();
        var jobs = await db.IotOtaJobs
            .AsNoTracking()
            .Include(x => x.Events)
            .OrderByDescending(x => x.RequestedAt)
            .ToListAsync(HttpContext.RequestAborted);
        return Ok(jobs.Select(x => new
        {
            job = ToOtaJobDto(x),
            events = x.Events.OrderBy(e => e.Sequence).Select(ToOtaEventDto)
        }));
    }

    [HttpGet("activity")]
    public async Task<IActionResult> ListActivity([FromQuery] int take = 100)
    {
        if (!await HasAccess(MenuPermission.Read))
            return Forbid();
        take = Math.Clamp(take, 1, 500);
        var events = await db.IotActivityEvents
            .AsNoTracking()
            .OrderByDescending(x => x.OccurredAt)
            .Take(take)
            .ToListAsync(HttpContext.RequestAborted);
        return Ok(events.Select(ToActivityDto));
    }

    private async Task<bool> HasAccess(MenuPermission permission)
    {
        if (await permissions.IsSuperAdminAsync(User))
            return true;
        return await permissions.HasPermissionAsync(User, "WAD_ECS_PERANGKAT", permission)
            || await permissions.HasPermissionAsync(User, "WAD_ECS_STASIUN", permission);
    }

    private async Task<IotDeviceAgent?> FindDevice(string deviceId)
    {
        if (Guid.TryParse(deviceId, out var id))
        {
            var byGuid = await db.IotDeviceAgents
                .Include(x => x.Credentials)
                .SingleOrDefaultAsync(x => x.Id == id, HttpContext.RequestAborted);
            if (byGuid is not null)
                return byGuid;
        }

        var normalized = deviceId.Trim().ToLower();
        return await db.IotDeviceAgents
            .Include(x => x.Credentials)
            .SingleOrDefaultAsync(x => x.DeviceId.ToLower() == normalized, HttpContext.RequestAborted);
    }

    private void AddActivity(Guid? deviceId, string type, string message)
    {
        db.IotActivityEvents.Add(new IotActivityEvent
        {
            DeviceAgentId = deviceId,
            EventType = type,
            Message = message,
            OccurredAt = DateTimeOffset.UtcNow
        });
    }

    private static bool IsCompatible(IotFirmwarePackage package, IotDeviceAgent device)
    {
        var boardProfiles = ParseJsonArray(package.CompatibleBoardProfilesJson);
        var revisions = ParseJsonArray(package.CompatibleHardwareRevisionsJson);
        var boardMatch = boardProfiles.Contains(device.BoardType, StringComparer.OrdinalIgnoreCase);
        var revisionMatch = revisions.Length == 0
            || revisions.Contains(device.BoardRevision, StringComparer.OrdinalIgnoreCase);
        return boardMatch && revisionMatch;
    }

    private static (string DeviceType, string Platform)? HardwareIdentityForBoard(string? boardType)
    {
        return boardType?.Trim().ToLowerInvariant() switch
        {
            "esp32-devkit-v1" => ("DATALOGGER", "ESP32"),
            "mega2560-esp8266" => ("MEGA_ESP8266", "ESP8266"),
            "esp8266-node" => ("ESP8266", "ESP8266"),
            _ => null
        };
    }

    private static string[] ParseList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Array.Empty<string>();
        if (value.TrimStart().StartsWith("[", StringComparison.Ordinal))
        {
            try
            {
                return JsonSerializer.Deserialize<string[]>(value) ?? Array.Empty<string>();
            }
            catch (JsonException)
            {
                return Array.Empty<string>();
            }
        }
        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string[] ParseJsonArray(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    private static string ToJson(JsonElement value) =>
        value.ValueKind == JsonValueKind.Undefined ? "{}" : value.GetRawText();

    private static IotDeviceDto ToDeviceDto(IotDeviceAgent x) => new()
    {
        Id = x.Id,
        DeviceId = x.DeviceId,
        Name = x.Name,
        DeviceType = x.DeviceType,
        Platform = x.Platform,
        BoardType = x.BoardType,
        BoardRevision = x.BoardRevision,
        Location = x.Location,
        Description = x.Description,
        Status = x.Status,
        IsActive = x.IsActive,
        LastSeenAt = x.LastSeenAt,
        LastFirmwareVersion = x.LastFirmwareVersion,
        LastIpAddress = x.LastIpAddress,
        LastRssi = x.LastRssi,
        LastUptimeSeconds = x.LastUptimeSeconds
    };

    private static IotConfigurationDto ToConfigurationDto(IotDeviceConfiguration x) => new()
    {
        Version = x.DesiredVersion,
        Configuration = ParseJsonElement(x.DesiredJson),
        Status = x.DesiredStatus,
        UpdatedAt = x.DesiredUpdatedAt
    };

    private static IotSensorTypeDto ToSensorTypeDto(IotSensorType x) => new()
    {
        Id = x.Id,
        Code = x.Code,
        Name = x.Name,
        Model = x.Model,
        Protocol = x.Protocol,
        Unit = x.Unit,
        Parameters = ParseJsonElement(x.ParametersJson),
        PinRequirements = ParseJsonElement(x.PinRequirementsJson),
        DriverKey = x.DriverKey,
        IsActive = x.IsActive
    };

    private static IotDeviceSensorDto ToDeviceSensorDto(IotDeviceSensor x) => new()
    {
        Id = x.Id,
        DeviceAgentId = x.DeviceAgentId,
        SensorTypeId = x.SensorTypeId,
        Label = x.Label,
        Protocol = x.Protocol,
        Unit = x.Unit,
        PinMappings = ParseJsonElement(x.PinMappingsJson),
        ReadIntervalSeconds = x.ReadIntervalSeconds,
        Calibration = x.CalibrationJson is null ? null : ParseJsonElement(x.CalibrationJson),
        IsEnabled = x.IsEnabled
    };

    private static IotFirmwareDto ToFirmwareDto(IotFirmwarePackage x) => new()
    {
        Id = x.Id,
        PackageName = x.PackageName,
        Version = x.Version,
        TargetMcu = x.TargetMcu,
        CompatibleBoardProfiles = ParseJsonArray(x.CompatibleBoardProfilesJson),
        Notes = x.Notes,
        FileName = x.FileName,
        SizeBytes = x.SizeBytes,
        Sha256 = x.Sha256,
        SignatureStatus = x.SignatureStatus,
        CreatedAt = x.CreatedAt
    };

    private static IotOtaJobDto ToOtaJobDto(IotOtaJob x) => new()
    {
        Id = x.Id,
        DeviceAgentId = x.DeviceAgentId,
        FirmwarePackageId = x.FirmwarePackageId,
        Status = x.Status,
        Progress = x.Progress,
        FailureReason = x.FailureReason,
        CurrentFirmwareVersion = x.CurrentFirmwareVersion,
        ReportedFirmwareVersion = x.ReportedFirmwareVersion,
        RequestedAt = x.RequestedAt,
        LastActivityAt = x.LastActivityAt
    };

    private static IotOtaEventDto ToOtaEventDto(IotOtaEvent x) => new()
    {
        Id = x.Id,
        OtaJobId = x.OtaJobId,
        Sequence = x.Sequence,
        EventType = x.EventType,
        Progress = x.Progress,
        Message = x.Message,
        ReportedFirmwareVersion = x.ReportedFirmwareVersion,
        OccurredAt = x.OccurredAt,
        ReceivedAt = x.ReceivedAt
    };

    private static IotActivityEventDto ToActivityDto(IotActivityEvent x) => new()
    {
        Id = x.Id,
        DeviceAgentId = x.DeviceAgentId,
        EventType = x.EventType,
        Message = x.Message,
        Details = x.DetailsJson is null ? null : ParseJsonElement(x.DetailsJson),
        OccurredAt = x.OccurredAt
    };

    private static JsonElement ParseJsonElement(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
