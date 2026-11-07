using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AuthUtility.Api.IoT;
using AuthUtility.Application.DTOs.IoT;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AuthUtility.Api.Controllers;

[ApiController]
[Route("api/iot/agent")]
public sealed class IotAgentController(
    AppDbContext db,
    IIotDeviceCredentialService credentials,
    IOptions<IotDeviceOptions> options,
    ILogger<IotAgentController> logger
) : ControllerBase
{
    [HttpPost("heartbeat")]
    public async Task<IActionResult> Heartbeat([FromBody] IotDeviceHeartbeatRequest request)
    {
        var authentication = await AuthenticateAsync();
        if (authentication.Error is not null)
            return authentication.Error;

        var device = authentication.Device!;
        var identityError = ValidateIdentity(device, request.DeviceId);
        if (identityError is not null)
            return identityError;

        if (request.DeviceTimestamp == default || string.IsNullOrWhiteSpace(request.FirmwareVersion))
        {
            return BadRequest(new { message = "deviceTimestamp dan firmwareVersion wajib diisi." });
        }

        var payloadJson = JsonSerializer.Serialize(request);
        if (!HasPayloadCapacity(payloadJson))
            return BadRequest(new { message = "Payload heartbeat melebihi batas." });

        var receivedAt = DateTimeOffset.UtcNow;
        var payloadHash = HashPayload(payloadJson);
        var duplicate = await db.IotDeviceReports
            .AsNoTracking()
            .AnyAsync(x => x.DeviceAgentId == device.Id
                && x.Kind == "heartbeat"
                && x.DeviceTimestamp == request.DeviceTimestamp
                && x.PayloadHash == payloadHash,
                HttpContext.RequestAborted);

        UpdateLastSeen(device, request.DeviceTimestamp, request.FirmwareVersion, request.IpAddress, request.Rssi, request.UptimeSeconds, receivedAt);

        if (!duplicate)
        {
            db.IotDeviceReports.Add(new IotDeviceReport
            {
                Id = 0,
                DeviceAgentId = device.Id,
                Kind = "heartbeat",
                DeviceTimestamp = request.DeviceTimestamp,
                ReceivedAt = receivedAt,
                FirmwareVersion = request.FirmwareVersion.Trim(),
                PayloadJson = payloadJson,
                PayloadHash = payloadHash
            });
        }

        await db.SaveChangesAsync(HttpContext.RequestAborted);

        var configurationVersion = await db.IotDeviceConfigurations
            .AsNoTracking()
            .Where(x => x.DeviceAgentId == device.Id)
            .Select(x => (int?)x.DesiredVersion)
            .SingleOrDefaultAsync(HttpContext.RequestAborted) ?? 0;
        var pendingOtaJobId = await db.IotOtaJobs
            .AsNoTracking()
            .Where(x => x.DeviceAgentId == device.Id
                && x.Status != IotOtaStatus.Succeeded
                && x.Status != IotOtaStatus.Failed)
            .OrderBy(x => x.RequestedAt)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(HttpContext.RequestAborted);

        return Ok(new IotDeviceHeartbeatResponse
        {
            ServerTime = receivedAt,
            ConfigurationVersion = configurationVersion,
            PendingOtaJobId = pendingOtaJobId
        });
    }

    [HttpPost("telemetry")]
    public async Task<IActionResult> Telemetry([FromBody] IotTelemetryRequest request)
    {
        var authentication = await AuthenticateAsync();
        if (authentication.Error is not null)
            return authentication.Error;

        var device = authentication.Device!;
        var identityError = ValidateIdentity(device, request.DeviceId);
        if (identityError is not null)
            return identityError;

        if (request.DeviceTimestamp == default || string.IsNullOrWhiteSpace(request.FirmwareVersion))
        {
            return BadRequest(new { message = "deviceTimestamp dan firmwareVersion wajib diisi." });
        }

        if (request.Readings.Count == 0 || request.Readings.Count > options.Value.MaxTelemetryReadings)
        {
            return BadRequest(new { message = "Jumlah readings telemetry tidak valid." });
        }

        foreach (var reading in request.Readings)
        {
            if (reading.Value.ValueKind != JsonValueKind.Number
                || !reading.Value.TryGetDouble(out var number)
                || !double.IsFinite(number))
            {
                return BadRequest(new { message = $"Nilai telemetry '{reading.Key}' harus berupa angka finite." });
            }
        }

        var payloadJson = request.RawPayload is { } rawPayload
            && rawPayload.ValueKind != JsonValueKind.Undefined
            ? rawPayload.GetRawText()
            : JsonSerializer.Serialize(request.Readings);
        if (!HasPayloadCapacity(payloadJson))
            return BadRequest(new { message = "Payload telemetry melebihi batas." });

        var payloadHash = HashPayload(payloadJson);
        var duplicate = await db.IotDeviceReports
            .AsNoTracking()
            .Where(x => x.DeviceAgentId == device.Id
                && x.Kind == "telemetry"
                && x.DeviceTimestamp == request.DeviceTimestamp
                && x.PayloadHash == payloadHash)
            .Select(x => (DateTimeOffset?)x.ReceivedAt)
            .SingleOrDefaultAsync(HttpContext.RequestAborted);

        if (duplicate.HasValue)
            return Ok(new IotTelemetryResponse(duplicate.Value));

        var receivedAt = DateTimeOffset.UtcNow;
        UpdateLastSeen(device, request.DeviceTimestamp, request.FirmwareVersion, null, null, null, receivedAt);
        db.IotDeviceReports.Add(new IotDeviceReport
        {
            DeviceAgentId = device.Id,
            Kind = "telemetry",
            DeviceTimestamp = request.DeviceTimestamp,
            ReceivedAt = receivedAt,
            FirmwareVersion = request.FirmwareVersion.Trim(),
            PayloadJson = payloadJson,
            PayloadHash = payloadHash
        });
        await db.SaveChangesAsync(HttpContext.RequestAborted);

        return Ok(new IotTelemetryResponse(receivedAt));
    }

    [HttpGet("configuration")]
    public async Task<IActionResult> GetConfiguration()
    {
        var authentication = await AuthenticateAsync();
        if (authentication.Error is not null)
            return authentication.Error;

        var configuration = await db.IotDeviceConfigurations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.DeviceAgentId == authentication.Device!.Id,
                HttpContext.RequestAborted);

        if (configuration is null)
        {
            return Ok(new IotConfigurationDto
            {
                Version = 0,
                Configuration = ParseJson("{}"),
                Status = IotConfigurationStatus.Applied,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        return Ok(ToConfigurationDto(configuration));
    }

    [HttpPost("configuration/{version:int}/ack")]
    public async Task<IActionResult> AcknowledgeConfiguration(
        int version,
        [FromBody] IotConfigurationAckRequest request)
    {
        var authentication = await AuthenticateAsync();
        if (authentication.Error is not null)
            return authentication.Error;

        var device = authentication.Device!;
        var identityError = ValidateIdentity(device, request.DeviceId);
        if (identityError is not null)
            return identityError;

        if (request.Status is not IotConfigurationStatus.Applied and not IotConfigurationStatus.Rejected)
        {
            return BadRequest(new { message = "Status configuration ACK tidak valid." });
        }

        var configuration = await db.IotDeviceConfigurations
            .SingleOrDefaultAsync(
                x => x.DeviceAgentId == device.Id,
                HttpContext.RequestAborted);
        if (configuration is null)
            return NotFound(new { message = "Configuration device belum tersedia." });

        if (configuration.DesiredVersion != version)
        {
            return Conflict(new
            {
                message = "Versi configuration sudah berubah.",
                currentVersion = configuration.DesiredVersion
            });
        }

        configuration.ReportedVersion = version;
        configuration.ReportedStatus = request.Status;
        configuration.ReportedAt = DateTimeOffset.UtcNow;
        configuration.RejectionReason = request.Status == IotConfigurationStatus.Rejected
            ? request.RejectionReason?.Trim()
            : null;
        if (request.Status == IotConfigurationStatus.Applied)
        {
            configuration.ReportedJson = configuration.DesiredJson;
            configuration.DesiredStatus = IotConfigurationStatus.Applied;
        }

        await db.SaveChangesAsync(HttpContext.RequestAborted);
        return Ok(ToConfigurationDto(configuration));
    }

    private async Task<(IotDeviceAgent? Device, IActionResult? Error)> AuthenticateAsync()
    {
        var key = Request.Headers["X-Device-Key"].FirstOrDefault();
        var authenticated = await credentials.AuthenticateAsync(key, HttpContext.RequestAborted);
        if (authenticated is not null)
            return (authenticated.Device, null);

        logger.LogWarning("IoT agent request rejected due to invalid device credential.");
        return (null, Unauthorized(new { message = "Device credential tidak valid." }));
    }

    private static IActionResult? ValidateIdentity(IotDeviceAgent device, string? requestDeviceId)
    {
        if (!string.Equals(device.DeviceId, requestDeviceId?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return new ConflictObjectResult(new
            {
                message = "deviceId tidak sesuai dengan credential device."
            });
        }

        return null;
    }

    private void UpdateLastSeen(
        IotDeviceAgent device,
        DateTimeOffset deviceTimestamp,
        string firmwareVersion,
        string? ipAddress,
        int? rssi,
        long? uptimeSeconds,
        DateTimeOffset receivedAt)
    {
        device.Status = IotDeviceStatus.Online;
        device.LastSeenAt = receivedAt;
        device.UpdatedAt = receivedAt;
        if (!device.LastDeviceTimestamp.HasValue || deviceTimestamp >= device.LastDeviceTimestamp)
        {
            device.LastDeviceTimestamp = deviceTimestamp;
            device.LastFirmwareVersion = firmwareVersion.Trim();
            device.LastIpAddress = ipAddress?.Trim();
            device.LastRssi = rssi;
            device.LastUptimeSeconds = uptimeSeconds;
        }
    }

    private bool HasPayloadCapacity(string payloadJson)
    {
        var limit = options.Value.MaxTelemetryBytes;
        return limit > 0 && Encoding.UTF8.GetByteCount(payloadJson) <= limit;
    }

    private static string HashPayload(string payloadJson) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson)))
            .ToLowerInvariant();

    private static IotConfigurationDto ToConfigurationDto(IotDeviceConfiguration configuration) =>
        new()
        {
            Version = configuration.DesiredVersion,
            Configuration = ParseJson(configuration.DesiredJson),
            Status = configuration.DesiredStatus,
            UpdatedAt = configuration.DesiredUpdatedAt
        };

    private static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
