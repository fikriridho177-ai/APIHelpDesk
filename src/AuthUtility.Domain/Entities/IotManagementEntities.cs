namespace AuthUtility.Domain.Entities;

public enum IotDeviceStatus
{
    Offline,
    Online,
    Stale,
    Disabled
}

public enum IotOtaStatus
{
    Queued,
    Downloading,
    Verifying,
    Installing,
    Rebooting,
    WaitingConfirmation,
    Succeeded,
    Failed
}

public enum IotOtaEventType
{
    Downloading,
    Verifying,
    Installing,
    Rebooting,
    WaitingConfirmation,
    Confirmed,
    Failed
}

public enum IotConfigurationStatus
{
    Pending,
    Applied,
    Rejected
}

public class IotDeviceAgent
{
    public Guid Id { get; set; }

    public string DeviceId { get; set; } = "";

    public string Name { get; set; } = "";

    public string DeviceType { get; set; } = "ESP8266";

    public string Platform { get; set; } = "";

    public string BoardType { get; set; } = "";

    public string BoardRevision { get; set; } = "";

    public string Location { get; set; } = "";

    public string? Description { get; set; }

    public IotDeviceStatus Status { get; set; } = IotDeviceStatus.Offline;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset? LastSeenAt { get; set; }

    public DateTimeOffset? LastDeviceTimestamp { get; set; }

    public string? LastFirmwareVersion { get; set; }

    public string? LastIpAddress { get; set; }

    public int? LastRssi { get; set; }

    public long? LastUptimeSeconds { get; set; }

    public string? CapabilitiesJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<IotDeviceCredential> Credentials { get; set; } = new List<IotDeviceCredential>();

    public ICollection<IotDeviceReport> Reports { get; set; } = new List<IotDeviceReport>();

    public ICollection<IotDeviceSensor> Sensors { get; set; } = new List<IotDeviceSensor>();

    public IotDeviceConfiguration? Configuration { get; set; }

    public ICollection<IotOtaJob> OtaJobs { get; set; } = new List<IotOtaJob>();

    public ICollection<IotActivityEvent> ActivityEvents { get; set; } = new List<IotActivityEvent>();
}

public class IotDeviceCredential
{
    public Guid Id { get; set; }

    public Guid DeviceAgentId { get; set; }

    public string KeyPrefix { get; set; } = "";

    public byte[] Salt { get; set; } = Array.Empty<byte>();

    public byte[] KeyHash { get; set; } = Array.Empty<byte>();

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? RevokedAt { get; set; }

    public IotDeviceAgent DeviceAgent { get; set; } = null!;
}

public class IotDeviceReport
{
    public long Id { get; set; }

    public Guid DeviceAgentId { get; set; }

    public string Kind { get; set; } = "telemetry";

    public DateTimeOffset DeviceTimestamp { get; set; }

    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;

    public string FirmwareVersion { get; set; } = "";

    public string PayloadJson { get; set; } = "{}";

    public string PayloadHash { get; set; } = "";

    public IotDeviceAgent DeviceAgent { get; set; } = null!;
}

public class IotSensorType
{
    public Guid Id { get; set; }

    public string Code { get; set; } = "";

    public string Name { get; set; } = "";

    public string? Model { get; set; }

    public string Protocol { get; set; } = "";

    public string Unit { get; set; } = "";

    public string ParametersJson { get; set; } = "{}";

    public string PinRequirementsJson { get; set; } = "{}";

    public string DriverKey { get; set; } = "";

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<IotDeviceSensor> DeviceSensors { get; set; } = new List<IotDeviceSensor>();
}

public class IotDeviceSensor
{
    public Guid Id { get; set; }

    public Guid DeviceAgentId { get; set; }

    public Guid SensorTypeId { get; set; }

    public string Label { get; set; } = "";

    public string Protocol { get; set; } = "";

    public string Unit { get; set; } = "";

    public string PinMappingsJson { get; set; } = "{}";

    public int ReadIntervalSeconds { get; set; } = 60;

    public string? CalibrationJson { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public IotDeviceAgent DeviceAgent { get; set; } = null!;

    public IotSensorType SensorType { get; set; } = null!;
}

public class IotDeviceConfiguration
{
    public Guid Id { get; set; }

    public Guid DeviceAgentId { get; set; }

    public int DesiredVersion { get; set; } = 1;

    public string DesiredJson { get; set; } = "{}";

    public IotConfigurationStatus DesiredStatus { get; set; } = IotConfigurationStatus.Pending;

    public DateTimeOffset DesiredUpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public int? ReportedVersion { get; set; }

    public string? ReportedJson { get; set; }

    public IotConfigurationStatus? ReportedStatus { get; set; }

    public DateTimeOffset? ReportedAt { get; set; }

    public string? RejectionReason { get; set; }

    public IotDeviceAgent DeviceAgent { get; set; } = null!;
}

public class IotFirmwarePackage
{
    public Guid Id { get; set; }

    public string PackageName { get; set; } = "";

    public string Version { get; set; } = "";

    public string TargetMcu { get; set; } = "ESP8266";

    public string CompatibleBoardProfilesJson { get; set; } = "[]";

    public string CompatibleHardwareRevisionsJson { get; set; } = "[]";

    public string? Notes { get; set; }

    public string FileName { get; set; } = "";

    public string StoragePath { get; set; } = "";

    public long SizeBytes { get; set; }

    public string Sha256 { get; set; } = "";

    public string SignatureStatus { get; set; } = "unsigned";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<IotOtaJob> OtaJobs { get; set; } = new List<IotOtaJob>();
}

public class IotOtaJob
{
    public Guid Id { get; set; }

    public Guid DeviceAgentId { get; set; }

    public Guid FirmwarePackageId { get; set; }

    public IotOtaStatus Status { get; set; } = IotOtaStatus.Queued;

    public int Progress { get; set; }

    public string? FailureReason { get; set; }

    public string? CurrentFirmwareVersion { get; set; }

    public string? ReportedFirmwareVersion { get; set; }

    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset LastActivityAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ConfirmedAt { get; set; }

    public IotDeviceAgent DeviceAgent { get; set; } = null!;

    public IotFirmwarePackage FirmwarePackage { get; set; } = null!;

    public ICollection<IotOtaEvent> Events { get; set; } = new List<IotOtaEvent>();
}

public class IotOtaEvent
{
    public long Id { get; set; }

    public Guid OtaJobId { get; set; }

    public int Sequence { get; set; }

    public IotOtaEventType EventType { get; set; }

    public int Progress { get; set; }

    public string? Message { get; set; }

    public string? ReportedFirmwareVersion { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;

    public IotOtaJob OtaJob { get; set; } = null!;
}

public class IotActivityEvent
{
    public long Id { get; set; }

    public Guid? DeviceAgentId { get; set; }

    public string EventType { get; set; } = "";

    public string Message { get; set; } = "";

    public string? DetailsJson { get; set; }

    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

    public IotDeviceAgent? DeviceAgent { get; set; }
}
