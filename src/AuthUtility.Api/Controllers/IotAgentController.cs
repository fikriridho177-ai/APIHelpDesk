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
    ILogger<IotAgentController> logger,
    IotFirmwareService? firmware = null
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

        var confirmingJob = await db.IotOtaJobs
            .Include(x => x.FirmwarePackage)
            .SingleOrDefaultAsync(x => x.DeviceAgentId == device.Id
                && (x.Status == IotOtaStatus.Installing
                    || x.Status == IotOtaStatus.Rebooting
                    || x.Status == IotOtaStatus.WaitingConfirmation)
                && x.FirmwarePackage.Version == request.FirmwareVersion.Trim(),
                HttpContext.RequestAborted);
        if (confirmingJob is not null)
        {
            confirmingJob.Status = IotOtaStatus.Succeeded;
            confirmingJob.Progress = 100;
            confirmingJob.ReportedFirmwareVersion = request.FirmwareVersion.Trim();
            confirmingJob.ConfirmedAt = receivedAt;
            confirmingJob.LastActivityAt = receivedAt;
            AddOtaEvent(confirmingJob, IotOtaEventType.Confirmed, 100, "Firmware target terkonfirmasi melalui heartbeat.", request.FirmwareVersion.Trim(), request.DeviceTimestamp, receivedAt);
        }

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

    [HttpGet("ota/pending")]
    public async Task<IActionResult> GetPendingOta([FromQuery] string? currentVersion = null)
    {
        var authentication = await AuthenticateAsync();
        if (authentication.Error is not null)
            return authentication.Error;

        var job = await db.IotOtaJobs
            .Include(x => x.FirmwarePackage)
            .Where(x => x.DeviceAgentId == authentication.Device!.Id
                && x.Status != IotOtaStatus.Succeeded
                && x.Status != IotOtaStatus.Failed)
            .OrderBy(x => x.RequestedAt)
            .FirstOrDefaultAsync(HttpContext.RequestAborted);
        if (job is null)
            return NoContent();
        if (!string.IsNullOrWhiteSpace(currentVersion)
            && string.Equals(currentVersion.Trim(), job.FirmwarePackage.Version, StringComparison.Ordinal))
        {
            return NoContent();
        }

        var firmwareUrl = Url.Action(
            nameof(DownloadFirmware),
            values: new { firmwareId = job.FirmwarePackageId })
            ?? $"/api/iot/agent/firmware/{job.FirmwarePackageId:D}/download";
        return Ok(new IotOtaPendingDto
        {
            JobId = job.Id,
            FirmwareId = job.FirmwarePackageId,
            TargetVersion = job.FirmwarePackage.Version,
            FirmwareUrl = firmwareUrl,
            Sha256 = job.FirmwarePackage.Sha256,
            SizeBytes = job.FirmwarePackage.SizeBytes
        });
    }

    [HttpPost("ota/{jobId:guid}/events")]
    public async Task<IActionResult> RecordOtaEvent(
        Guid jobId,
        [FromBody] IotOtaEventRequest request)
    {
        var authentication = await AuthenticateAsync();
        if (authentication.Error is not null)
            return authentication.Error;

        var job = await db.IotOtaJobs
            .Include(x => x.FirmwarePackage)
            .SingleOrDefaultAsync(x => x.Id == jobId, HttpContext.RequestAborted);
        if (job is null || job.DeviceAgentId != authentication.Device!.Id)
            return NotFound(new { message = "OTA job tidak ditemukan." });
        var identityError = ValidateIdentity(authentication.Device, request.DeviceId);
        if (identityError is not null)
            return identityError;
        if (request.EventType == IotOtaEventType.Confirmed)
            return Conflict(new { message = "Gunakan endpoint confirm untuk memverifikasi versi firmware." });

        if (!IotOtaRules.TryTransition(job.Status, request.EventType, request.Progress, out var next, out var error))
        {
            return Conflict(new { message = error });
        }

        var receivedAt = DateTimeOffset.UtcNow;
        var occurredAt = request.OccurredAt == default ? receivedAt : request.OccurredAt;
        job.Status = next;
        job.Progress = request.Progress;
        job.LastActivityAt = receivedAt;
        if (request.ReportedFirmwareVersion is not null)
            job.ReportedFirmwareVersion = request.ReportedFirmwareVersion.Trim();
        if (next == IotOtaStatus.Failed)
            job.FailureReason = string.IsNullOrWhiteSpace(request.Message) ? "Perangkat melaporkan OTA gagal." : request.Message.Trim();
        AddOtaEvent(job, request.EventType, request.Progress, request.Message, request.ReportedFirmwareVersion, occurredAt, receivedAt);
        await db.SaveChangesAsync(HttpContext.RequestAborted);
        return Ok(new { jobId = job.Id, status = job.Status, progress = job.Progress });
    }

    [HttpPost("ota/{jobId:guid}/confirm")]
    public async Task<IActionResult> ConfirmOta(
        Guid jobId,
        [FromBody] IotOtaConfirmRequest request)
    {
        var authentication = await AuthenticateAsync();
        if (authentication.Error is not null)
            return authentication.Error;

        var job = await db.IotOtaJobs
            .Include(x => x.FirmwarePackage)
            .SingleOrDefaultAsync(x => x.Id == jobId, HttpContext.RequestAborted);
        if (job is null || job.DeviceAgentId != authentication.Device!.Id)
            return NotFound(new { message = "OTA job tidak ditemukan." });
        var identityError = ValidateIdentity(authentication.Device, request.DeviceId);
        if (identityError is not null)
            return identityError;
        if (job.Status == IotOtaStatus.Succeeded)
        {
            if (!IotOtaRules.TryConfirm(job.FirmwarePackage.Version, request.ReportedFirmwareVersion, out var alreadyConfirmedError))
                return Conflict(new { message = alreadyConfirmedError });

            return Ok(new { jobId = job.Id, status = job.Status, progress = job.Progress });
        }
        if (job.Status != IotOtaStatus.WaitingConfirmation)
            return Conflict(new { message = "OTA belum berada pada tahap waiting_confirmation." });

        if (!IotOtaRules.TryConfirm(job.FirmwarePackage.Version, request.ReportedFirmwareVersion, out var error))
        {
            var receivedAt = DateTimeOffset.UtcNow;
            job.Status = IotOtaStatus.Failed;
            job.FailureReason = error;
            job.LastActivityAt = receivedAt;
            AddOtaEvent(job, IotOtaEventType.Failed, job.Progress, error, request.ReportedFirmwareVersion, request.ReportedAt, receivedAt);
            await db.SaveChangesAsync(HttpContext.RequestAborted);
            return Conflict(new { message = error });
        }

        var confirmedAt = DateTimeOffset.UtcNow;
        job.Status = IotOtaStatus.Succeeded;
        job.Progress = 100;
        job.ReportedFirmwareVersion = request.ReportedFirmwareVersion.Trim();
        job.ConfirmedAt = confirmedAt;
        job.LastActivityAt = confirmedAt;
        AddOtaEvent(job, IotOtaEventType.Confirmed, 100, "Firmware target terkonfirmasi.", request.ReportedFirmwareVersion.Trim(), request.ReportedAt, confirmedAt);
        await db.SaveChangesAsync(HttpContext.RequestAborted);
        return Ok(new { jobId = job.Id, status = job.Status, progress = job.Progress });
    }

    [HttpGet("firmware/{firmwareId:guid}/download")]
    public async Task<IActionResult> DownloadFirmware(Guid firmwareId)
    {
        var authentication = await AuthenticateAsync();
        if (authentication.Error is not null)
            return authentication.Error;

        var assigned = await db.IotOtaJobs
            .Include(x => x.FirmwarePackage)
            .Where(x => x.DeviceAgentId == authentication.Device!.Id
                && x.FirmwarePackageId == firmwareId
                && x.Status != IotOtaStatus.Succeeded
                && x.Status != IotOtaStatus.Failed)
            .OrderBy(x => x.RequestedAt)
            .FirstOrDefaultAsync(HttpContext.RequestAborted);
        if (assigned is null)
            return Forbid();

        try
        {
            var service = firmware ?? new IotFirmwareService(db);
            var path = service.GetSafeFilePath(assigned.FirmwarePackage);
            if (!System.IO.File.Exists(path))
                return NotFound(new { message = "File firmware tidak ditemukan." });
            return PhysicalFile(path, "application/octet-stream", assigned.FirmwarePackage.FileName, enableRangeProcessing: true);
        }
        catch (IotFirmwareValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
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

    private void AddOtaEvent(
        IotOtaJob job,
        IotOtaEventType eventType,
        int progress,
        string? message,
        string? reportedFirmwareVersion,
        DateTimeOffset occurredAt,
        DateTimeOffset receivedAt)
    {
        var sequence = job.Events.Count == 0
            ? db.IotOtaEvents
                .Where(x => x.OtaJobId == job.Id)
                .Select(x => (int?)x.Sequence)
                .Max() ?? 0
            : job.Events.Max(x => x.Sequence);
        job.Events.Add(new IotOtaEvent
        {
            OtaJobId = job.Id,
            Sequence = sequence + 1,
            EventType = eventType,
            Progress = progress,
            Message = message?.Trim(),
            ReportedFirmwareVersion = reportedFirmwareVersion?.Trim(),
            OccurredAt = occurredAt == default ? receivedAt : occurredAt,
            ReceivedAt = receivedAt
        });
    }
}
