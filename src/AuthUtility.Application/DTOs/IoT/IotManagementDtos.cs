using System.Text.Json;
using AuthUtility.Domain.Entities;

namespace AuthUtility.Application.DTOs.IoT;

public sealed record IotDeviceDto
{
    public Guid Id { get; init; }
    public string DeviceId { get; init; } = "";
    public string Name { get; init; } = "";
    public string DeviceType { get; init; } = "";
    public string Platform { get; init; } = "";
    public string BoardType { get; init; } = "";
    public string BoardRevision { get; init; } = "";
    public string Location { get; init; } = "";
    public string? Description { get; init; }
    public IotDeviceStatus Status { get; init; }
    public bool IsActive { get; init; }
    public DateTimeOffset? LastSeenAt { get; init; }
    public string? LastFirmwareVersion { get; init; }
    public string? LastIpAddress { get; init; }
    public int? LastRssi { get; init; }
    public long? LastUptimeSeconds { get; init; }
}

public sealed record IotDeviceProvisionRequest
{
    public string DeviceId { get; init; } = "";
    public string Name { get; init; } = "";
    public string DeviceType { get; init; } = "ESP8266";
    public string Platform { get; init; } = "ESP8266";
    public string BoardType { get; init; } = "";
    public string BoardRevision { get; init; } = "";
    public string Location { get; init; } = "";
    public string? Description { get; init; }
}

public sealed record IotDeviceProvisionResponse(IotDeviceDto Device, string DeviceKey);

public sealed record IotDeviceUpdateRequest
{
    public string Name { get; init; } = "";
    public string BoardType { get; init; } = "";
    public string BoardRevision { get; init; } = "";
    public string Location { get; init; } = "";
    public string? Description { get; init; }
    public bool? IsActive { get; init; }
}

public sealed record IotDeviceHeartbeatRequest
{
    public string DeviceId { get; init; } = "";
    public DateTimeOffset DeviceTimestamp { get; init; }
    public string FirmwareVersion { get; init; } = "";
    public string? IpAddress { get; init; }
    public int? Rssi { get; init; }
    public long? UptimeSeconds { get; init; }
    public Dictionary<string, JsonElement>? Capabilities { get; init; }
}

public sealed record IotDeviceHeartbeatResponse
{
    public DateTimeOffset ServerTime { get; init; }
    public int ConfigurationVersion { get; init; }
    public Guid? PendingOtaJobId { get; init; }
}

public sealed record IotTelemetryRequest
{
    public string DeviceId { get; init; } = "";
    public DateTimeOffset DeviceTimestamp { get; init; }
    public string FirmwareVersion { get; init; } = "";
    public Dictionary<string, JsonElement> Readings { get; init; } = new();
    public JsonElement? RawPayload { get; init; }
}

public sealed record IotTelemetryResponse(DateTimeOffset ReceivedAt);

public sealed record IotConfigurationDto
{
    public int Version { get; init; }
    public JsonElement Configuration { get; init; }
    public IotConfigurationStatus Status { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed record IotConfigurationAckRequest
{
    public string DeviceId { get; init; } = "";
    public IotConfigurationStatus Status { get; init; }
    public string? RejectionReason { get; init; }
}

public sealed record IotFirmwareDto
{
    public Guid Id { get; init; }
    public string PackageName { get; init; } = "";
    public string Version { get; init; } = "";
    public string TargetMcu { get; init; } = "";
    public string FileName { get; init; } = "";
    public long SizeBytes { get; init; }
    public string Sha256 { get; init; } = "";
    public string SignatureStatus { get; init; } = "";
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed record IotOtaJobDto
{
    public Guid Id { get; init; }
    public Guid DeviceAgentId { get; init; }
    public Guid FirmwarePackageId { get; init; }
    public IotOtaStatus Status { get; init; }
    public int Progress { get; init; }
    public string? FailureReason { get; init; }
    public string? CurrentFirmwareVersion { get; init; }
    public string? ReportedFirmwareVersion { get; init; }
    public DateTimeOffset RequestedAt { get; init; }
    public DateTimeOffset LastActivityAt { get; init; }
}

public sealed record IotOtaPendingDto
{
    public Guid JobId { get; init; }
    public IotFirmwareDto Firmware { get; init; } = new();
    public string DownloadUrl { get; init; } = "";
    public string Sha256 { get; init; } = "";
    public long SizeBytes { get; init; }
}

public sealed record IotOtaEventRequest
{
    public IotOtaEventType EventType { get; init; }
    public int Progress { get; init; }
    public string? Message { get; init; }
    public string? ReportedFirmwareVersion { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
}

public sealed record IotOtaConfirmRequest
{
    public string ReportedFirmwareVersion { get; init; } = "";
    public DateTimeOffset ReportedAt { get; init; }
}

public sealed record IotSensorTypeDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public string? Model { get; init; }
    public string Protocol { get; init; } = "";
    public string Unit { get; init; } = "";
    public JsonElement Parameters { get; init; }
    public JsonElement PinRequirements { get; init; }
    public string DriverKey { get; init; } = "";
    public bool IsActive { get; init; }
}

public sealed record IotDeviceSensorDto
{
    public Guid Id { get; init; }
    public Guid DeviceAgentId { get; init; }
    public Guid SensorTypeId { get; init; }
    public string Label { get; init; } = "";
    public string Protocol { get; init; } = "";
    public string Unit { get; init; } = "";
    public JsonElement PinMappings { get; init; }
    public int ReadIntervalSeconds { get; init; }
    public JsonElement? Calibration { get; init; }
    public bool IsEnabled { get; init; }
}

public sealed record IotActivityEventDto
{
    public long Id { get; init; }
    public Guid? DeviceAgentId { get; init; }
    public string EventType { get; init; } = "";
    public string Message { get; init; } = "";
    public JsonElement? Details { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
}
