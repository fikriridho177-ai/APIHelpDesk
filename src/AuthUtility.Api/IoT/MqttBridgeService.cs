using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace AuthUtility.Api.IoT;

public sealed class IotMqttOptions
{
    public string BrokerUrl { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string TopicPrefix { get; set; } = "rsup/wad";
    public bool Enabled => !string.IsNullOrWhiteSpace(BrokerUrl);
}

public interface IIotMqttPublisher
{
    bool IsConnected { get; }

    Task<bool> PublishOtaCommandAsync(
        string deviceId,
        IReadOnlyDictionary<string, object?> payload,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Jembatan MQTT dua arah: topik perangkat diperbarui ke PostgreSQL dan
/// perintah OTA dipublikasikan dari API ke topik perangkat.
/// </summary>
public sealed class MqttBridgeService(
    IServiceScopeFactory scopeFactory,
    IOptions<IotMqttOptions> options,
    ILogger<MqttBridgeService> logger
) : BackgroundService, IIotMqttPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IotMqttOptions _options = options.Value;
    private readonly SemaphoreSlim _dbGate = new(1, 1);
    private readonly ConcurrentDictionary<string, byte> _loggedUnknownDevices = new();
    private IMqttClient? _client;

    public bool IsConnected => _client?.IsConnected == true;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation(
                "MQTT bridge nonaktif. Set MQTT_BROKER_URL untuk menghubungkan broker {Prefix}/devices/+/status.",
                Prefix);
            return;
        }

        var factory = new MqttFactory();
        _client = factory.CreateMqttClient();
        _client.ApplicationMessageReceivedAsync += context => HandleMessageAsync(context);
        _client.DisconnectedAsync += context =>
        {
            logger.LogWarning("MQTT bridge terputus: {Reason}", context.Reason);
            return Task.CompletedTask;
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_client.IsConnected)
            {
                await DelayAsync(TimeSpan.FromSeconds(10), stoppingToken);
                continue;
            }

            try
            {
                var clientOptions = BuildClientOptions();
                await _client.ConnectAsync(clientOptions, stoppingToken);

                var subscribe = new MqttClientSubscribeOptionsBuilder()
                    .WithTopicFilter(f => f.WithTopic($"{Prefix}/devices/+/status").WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce))
                    .WithTopicFilter(f => f.WithTopic($"{Prefix}/devices/+/telemetry").WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce))
                    .WithTopicFilter(f => f.WithTopic($"{Prefix}/devices/+/ota/status").WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce))
                    .Build();

                await _client.SubscribeAsync(subscribe, stoppingToken);
                logger.LogInformation(
                    "MQTT bridge terhubung ke {Broker} dan berlangganan {Prefix}/devices/+/status|telemetry|ota/status.",
                    _options.BrokerUrl,
                    Prefix);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "MQTT bridge gagal terhubung ke {Broker}; mencoba lagi.", _options.BrokerUrl);
            }

            await DelayAsync(TimeSpan.FromSeconds(5), stoppingToken);
        }

        if (_client?.IsConnected == true)
        {
            try { await _client.DisconnectAsync(); }
            catch (Exception ex) { logger.LogDebug(ex, "MQTT disconnect saat shutdown gagal."); }
        }
    }

    public async Task<bool> PublishOtaCommandAsync(
        string deviceId,
        IReadOnlyDictionary<string, object?> payload,
        CancellationToken cancellationToken = default)
    {
        if (_client?.IsConnected != true)
        {
            logger.LogWarning(
                "Perintah OTA untuk {DeviceId} tidak terkirim: MQTT bridge tidak terhubung.",
                deviceId);
            return false;
        }

        try
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic($"{Prefix}/devices/{deviceId}/ota/cmd")
                .WithPayload(JsonSerializer.Serialize(payload, JsonOptions))
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await _client.PublishAsync(message, cancellationToken);
            logger.LogInformation("Perintah OTA dipublikasikan untuk {DeviceId}.", deviceId);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Gagal mempublikasikan perintah OTA untuk {DeviceId}.", deviceId);
            return false;
        }
    }

    private string Prefix => _options.TopicPrefix.Trim().Trim('/');

    private MqttClientOptions BuildClientOptions()
    {
        var raw = _options.BrokerUrl.Trim();
        var secure = raw.StartsWith("mqtts://", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("ssl://", StringComparison.OrdinalIgnoreCase);
        var normalized = raw
            .Replace("mqtt://", "tcp://", StringComparison.OrdinalIgnoreCase)
            .Replace("mqtts://", "ssl://", StringComparison.OrdinalIgnoreCase);
        var uri = new Uri(normalized);
        var port = uri.Port > 0 ? uri.Port : secure ? 8883 : 1883;

        var builder = new MqttClientOptionsBuilder()
            .WithClientId($"helpdesk-api-{Environment.MachineName}")
            .WithTcpServer(uri.Host, port)
            .WithCleanSession()
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(30))
            .WithTimeout(TimeSpan.FromSeconds(15));

        if (!string.IsNullOrWhiteSpace(_options.Username))
            builder = builder.WithCredentials(_options.Username, _options.Password);

        if (secure)
            builder = builder.WithTlsOptions(tls => tls.UseTls());

        return builder.Build();
    }

    private Task HandleMessageAsync(MqttApplicationMessageReceivedEventArgs context)
    {
        var topic = context.ApplicationMessage.Topic;
        var payload = context.ApplicationMessage.ConvertPayloadToString();
        var deviceId = ExtractDeviceId(topic);
        if (deviceId is null || string.IsNullOrWhiteSpace(payload))
            return Task.CompletedTask;

        _ = Task.Run(async () =>
        {
            await _dbGate.WaitAsync();
            try
            {
                if (topic.EndsWith("/ota/status", StringComparison.OrdinalIgnoreCase))
                    await ApplyOtaStatusAsync(deviceId, payload);
                else if (topic.EndsWith("/telemetry", StringComparison.OrdinalIgnoreCase))
                    await ApplyTelemetryAsync(deviceId, payload);
                else if (topic.EndsWith("/status", StringComparison.OrdinalIgnoreCase))
                    await ApplyStatusAsync(deviceId, payload);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Gagal memproses pesan MQTT {Topic}.", topic);
            }
            finally
            {
                _dbGate.Release();
            }
        });

        return Task.CompletedTask;
    }

    private string? ExtractDeviceId(string topic)
    {
        var marker = $"{Prefix}/devices/";
        if (!topic.StartsWith(marker, StringComparison.OrdinalIgnoreCase))
            return null;

        var rest = topic[marker.Length..];
        var slash = rest.IndexOf('/');
        if (slash <= 0)
            return null;

        return Uri.UnescapeDataString(rest[..slash]);
    }

    private async Task ApplyStatusAsync(string mqttDeviceId, string payload)
    {
        using var json = ParseJson(payload);
        if (json is null) return;

        var deviceId = ReadString(json.RootElement, "device_id", "deviceId") ?? mqttDeviceId;
        var status = ReadString(json.RootElement, "status")?.Trim().ToLowerInvariant() ?? "offline";
        var isOnline = status is "online" or "connected" or "up";

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await FindDeviceAsync(db, deviceId);
        if (device is null) { LogUnknown(deviceId); return; }

        var now = DateTimeOffset.UtcNow;
        var previous = device.Status;
        device.Status = isOnline
            ? IotDeviceStatus.Online
            : device.IsActive ? IotDeviceStatus.Offline : IotDeviceStatus.Disabled;
        device.UpdatedAt = now;

        if (isOnline)
        {
            device.LastSeenAt = now;
            device.LastFirmwareVersion = ReadString(json.RootElement, "fw_version", "firmware", "firmware_version") ?? device.LastFirmwareVersion;
            device.LastIpAddress = ReadString(json.RootElement, "ip", "ip_address") ?? device.LastIpAddress;
            device.LastRssi = ReadInt(json.RootElement, "rssi") ?? device.LastRssi;
        }

        if (previous != device.Status)
        {
            db.IotActivityEvents.Add(new IotActivityEvent
            {
                DeviceAgentId = device.Id,
                EventType = "mqtt-status",
                Message = isOnline
                    ? $"Perangkat {device.DeviceId} online via MQTT."
                    : $"Perangkat {device.DeviceId} offline (LWT/disconnect).",
                OccurredAt = now
            });
        }

        if (isOnline)
            await ConfirmWaitingOtaAsync(db, device, device.LastFirmwareVersion, now);

        await db.SaveChangesAsync();
    }

    private async Task ApplyTelemetryAsync(string mqttDeviceId, string payload)
    {
        using var json = ParseJson(payload);
        if (json is null) return;

        var deviceId = ReadString(json.RootElement, "device_id", "deviceId") ?? mqttDeviceId;

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await FindDeviceAsync(db, deviceId);
        if (device is null) { LogUnknown(deviceId); return; }

        var now = DateTimeOffset.UtcNow;
        var firmware = ReadString(json.RootElement, "fw_version", "firmware", "firmware_version");
        device.Status = IotDeviceStatus.Online;
        device.LastSeenAt = now;
        device.UpdatedAt = now;
        device.LastFirmwareVersion = firmware ?? device.LastFirmwareVersion;
        device.LastUptimeSeconds = ReadLong(json.RootElement, "uptime_sec", "uptimeSeconds", "uptime") ?? device.LastUptimeSeconds;
        device.LastRssi = ReadInt(json.RootElement, "rssi") ?? device.LastRssi;
        device.LastBatteryVolt = ReadDouble(json.RootElement, "battery_volt", "batteryVolt") ?? device.LastBatteryVolt;

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        db.IotDeviceReports.Add(new IotDeviceReport
        {
            DeviceAgentId = device.Id,
            Kind = "mqtt-telemetry",
            DeviceTimestamp = now,
            ReceivedAt = now,
            FirmwareVersion = device.LastFirmwareVersion ?? "",
            PayloadJson = payload,
            PayloadHash = hash
        });

        await ConfirmWaitingOtaAsync(db, device, device.LastFirmwareVersion, now);
        await db.SaveChangesAsync();
    }

    private async Task ApplyOtaStatusAsync(string mqttDeviceId, string payload)
    {
        using var json = ParseJson(payload);
        if (json is null) return;

        var deviceId = ReadString(json.RootElement, "device_id", "deviceId") ?? mqttDeviceId;
        var state = (ReadString(json.RootElement, "state") ?? "").Trim().ToUpperInvariant();
        var progress = Math.Clamp(ReadInt(json.RootElement, "progress") ?? 0, 0, 100);
        var error = ReadString(json.RootElement, "error") ?? "";
        var jobIdRaw = json.RootElement.TryGetProperty("job_id", out var jobElement)
            || json.RootElement.TryGetProperty("jobId", out jobElement)
            ? jobElement
            : default;

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await FindDeviceAsync(db, deviceId);
        if (device is null) { LogUnknown(deviceId); return; }

        var job = await FindOtaJobAsync(db, jobIdRaw, device.Id);
        if (job is null)
        {
            logger.LogWarning("OTA status untuk job {Job} tidak ditemukan (device {DeviceId}).", jobIdRaw, deviceId);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var previousStatus = job.Status;
        var previousProgress = job.Progress;
        IotOtaEventType? eventType = null;
        string? message = string.IsNullOrWhiteSpace(error) ? null : error.Trim();

        switch (state)
        {
            case "DOWNLOADING":
                job.Status = IotOtaStatus.Downloading;
                eventType = IotOtaEventType.Downloading;
                message ??= "Firmware sedang diunduh oleh perangkat.";
                break;
            case "FLASHING":
            case "INSTALLING":
                job.Status = IotOtaStatus.Installing;
                eventType = IotOtaEventType.Installing;
                message ??= "Firmware ditulis ke slot OTA.";
                break;
            case "REBOOTING":
                job.Status = IotOtaStatus.Rebooting;
                eventType = IotOtaEventType.Rebooting;
                message ??= "Perangkat restart ke firmware baru.";
                break;
            case "SUCCESS":
                job.Status = IotOtaStatus.Succeeded;
                job.Progress = 100;
                job.ReportedFirmwareVersion = job.FirmwarePackage?.Version;
                job.ConfirmedAt = now;
                eventType = IotOtaEventType.Confirmed;
                message ??= "Firmware baru berjalan dan dikonfirmasi perangkat.";
                if (job.FirmwarePackage is not null)
                {
                    device.LastFirmwareVersion = job.FirmwarePackage.Version;
                    device.LastSeenAt = now;
                }
                break;
            case "FAILED":
                job.Status = IotOtaStatus.Failed;
                job.FailureReason = string.IsNullOrWhiteSpace(error) ? "Perangkat melaporkan OTA gagal." : error.Trim();
                eventType = IotOtaEventType.Failed;
                message = job.FailureReason;
                break;
            default:
                logger.LogDebug("OTA state {State} dari {DeviceId} diabaikan.", state, deviceId);
                return;
        }

        job.Progress = Math.Clamp(progress, 0, 100);
        job.LastActivityAt = now;

        var statusChanged = job.Status != previousStatus;
        var progressChanged = job.Progress != previousProgress;
        if (eventType is not null && (statusChanged || progressChanged))
        {
            var lastSequence = await db.IotOtaEvents
                .Where(x => x.OtaJobId == job.Id)
                .MaxAsync(x => (int?)x.Sequence) ?? 0;
            db.IotOtaEvents.Add(new IotOtaEvent
            {
                OtaJobId = job.Id,
                Sequence = lastSequence + 1,
                EventType = eventType.Value,
                Progress = job.Progress,
                Message = message,
                ReportedFirmwareVersion = job.ReportedFirmwareVersion,
                OccurredAt = now,
                ReceivedAt = now
            });
        }

        db.IotActivityEvents.Add(new IotActivityEvent
        {
            DeviceAgentId = device.Id,
            EventType = "ota-mqtt",
            Message = $"OTA {state} {job.Progress}% dari {device.DeviceId}.",
            OccurredAt = now
        });

        await db.SaveChangesAsync();
    }

    private static async Task ConfirmWaitingOtaAsync(
        AppDbContext db,
        IotDeviceAgent device,
        string? reportedVersion,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(reportedVersion))
            return;

        var waiting = await db.IotOtaJobs
            .Include(x => x.FirmwarePackage)
            .Where(x => x.DeviceAgentId == device.Id && x.Status == IotOtaStatus.WaitingConfirmation)
            .OrderByDescending(x => x.RequestedAt)
            .FirstOrDefaultAsync();
        if (waiting is null)
            return;

        var target = waiting.FirmwarePackage?.Version;
        if (string.IsNullOrWhiteSpace(target))
            return;

        if (!string.Equals(target.Trim(), reportedVersion.Trim(), StringComparison.OrdinalIgnoreCase))
            return;

        waiting.Status = IotOtaStatus.Succeeded;
        waiting.Progress = 100;
        waiting.ReportedFirmwareVersion = reportedVersion.Trim();
        waiting.ConfirmedAt = now;
        waiting.LastActivityAt = now;
        var lastSequence = await db.IotOtaEvents
            .Where(x => x.OtaJobId == waiting.Id)
            .MaxAsync(x => (int?)x.Sequence) ?? 0;
        db.IotOtaEvents.Add(new IotOtaEvent
        {
            OtaJobId = waiting.Id,
            Sequence = lastSequence + 1,
            EventType = IotOtaEventType.Confirmed,
            Progress = 100,
            Message = "Heartbeat pascareboot mengonfirmasi versi target.",
            ReportedFirmwareVersion = reportedVersion.Trim(),
            OccurredAt = now,
            ReceivedAt = now
        });
    }

    private static async Task<IotDeviceAgent?> FindDeviceAsync(AppDbContext db, string deviceId) =>
        await db.IotDeviceAgents.FirstOrDefaultAsync(x => x.DeviceId.ToLower() == deviceId.ToLower());

    private static async Task<IotOtaJob?> FindOtaJobAsync(AppDbContext db, JsonElement jobId, Guid deviceAgentId)
    {
        IQueryable<IotOtaJob> query = db.IotOtaJobs
            .Include(x => x.FirmwarePackage)
            .Where(x => x.DeviceAgentId == deviceAgentId);

        if (jobId.ValueKind == JsonValueKind.Number && jobId.TryGetInt64(out var number))
            return await query.FirstOrDefaultAsync(x => x.JobNumber == number);

        if (jobId.ValueKind == JsonValueKind.String)
        {
            var text = jobId.GetString();
            if (long.TryParse(text, out number))
                return await query.FirstOrDefaultAsync(x => x.JobNumber == number);
            if (Guid.TryParse(text, out var guid))
                return await query.FirstOrDefaultAsync(x => x.Id == guid);
        }

        return null;
    }

    private static JsonDocument? ParseJson(string payload)
    {
        try { return JsonDocument.Parse(payload); }
        catch (JsonException) { return null; }
    }

    private static string? ReadString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value)) continue;
            if (value.ValueKind == JsonValueKind.String) return value.GetString();
            if (value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                return value.ToString();
        }
        return null;
    }

    private static int? ReadInt(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value)) continue;
            if (value.ValueKind == JsonValueKind.Number)
            {
                if (value.TryGetInt32(out var number)) return number;
                if (value.TryGetDouble(out var real)) return (int)Math.Round(real);
            }
            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed))
                return parsed;
        }
        return null;
    }

    private static long? ReadLong(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value)) continue;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)) return number;
            if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out var parsed)) return parsed;
        }
        return null;
    }

    private static double? ReadDouble(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value)) continue;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number)) return number;
            if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), out var parsed)) return parsed;
        }
        return null;
    }

    private void LogUnknown(string deviceId)
    {
        if (_loggedUnknownDevices.TryAdd(deviceId, 0))
            logger.LogWarning("Pesan MQTT dari device {DeviceId} diabaikan: perangkat belum terdaftar.", deviceId);
    }

    private static async Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try { await Task.Delay(delay, cancellationToken); }
        catch (OperationCanceledException) { }
    }
}
