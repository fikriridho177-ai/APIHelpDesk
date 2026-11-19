using System.Text.Json;
using AuthUtility.Application.DTOs.IoT;

namespace AuthUtility.Api.IoT;

/// <summary>
/// Snapshot hardware yang diterima dari perangkat. Nilai dibatasi agar payload
/// anonim (discovery) atau heartbeat tidak dapat menyimpan JSON sembarang.
/// </summary>
public static class IotHardwareSnapshot
{
    private static readonly string[] CapabilityKeys =
    [
        "mqtt",
        "sht35",
        "soilMoisture",
        "ultrasonic",
        "rtcDs3231",
        "zhafiraRainGauge",
        "nativeHttpsOta",
        "sha256Verification"
    ];

    private static readonly HashSet<string> SensorStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "responsive",
        "no_response",
        "invalid_data",
        "stale_data",
        "unverified",
        "disabled"
    };

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string CreateJson(
        string? macAddress,
        string? chipId,
        string? chipModel,
        int? chipRevision,
        string? deviceType,
        string? platform,
        string? boardType,
        string? boardRevision,
        string? firmwareVersion,
        string? ipAddress,
        int? rssi,
        long? uptimeSeconds,
        IReadOnlyDictionary<string, JsonElement>? capabilities,
        IReadOnlyList<IotHardwareSensorDto>? sensors)
    {
        var capabilityValues = CapabilityKeys.ToDictionary(
            key => key,
            key => ReadCapability(capabilities, key),
            StringComparer.Ordinal);

        var sensorValues = (sensors ?? Array.Empty<IotHardwareSensorDto>())
            .Take(16)
            .Select(NormalizeSensor)
            .Where(sensor => sensor["key"] is not null)
            .ToList();

        var envelope = new Dictionary<string, object?>
        {
            ["connection"] = new Dictionary<string, object?>
            {
                ["macAddress"] = Clean(macAddress, 64),
                ["chipId"] = Clean(chipId, 64),
                ["chipModel"] = Clean(chipModel, 64),
                ["chipRevision"] = chipRevision,
                ["ipAddress"] = Clean(ipAddress, 64),
                ["rssi"] = rssi,
                ["uptimeSeconds"] = uptimeSeconds
            },
            ["firmware"] = new Dictionary<string, object?>
            {
                ["version"] = Clean(firmwareVersion, 64),
                ["deviceType"] = Clean(deviceType, 64),
                ["platform"] = Clean(platform, 64),
                ["boardType"] = Clean(boardType, 64),
                ["boardRevision"] = Clean(boardRevision, 64)
            },
            ["capabilities"] = capabilityValues,
            ["sensors"] = sensorValues
        };

        return JsonSerializer.Serialize(envelope, SerializerOptions);
    }

    private static Dictionary<string, object?> NormalizeSensor(IotHardwareSensorDto sensor)
    {
        var status = SensorStatuses.Contains(sensor.Status)
            ? sensor.Status.Trim().ToLowerInvariant()
            : "unverified";

        return new Dictionary<string, object?>
        {
            ["key"] = Clean(sensor.Key, 32),
            ["label"] = Clean(sensor.Label, 64),
            ["interface"] = Clean(sensor.Interface, 32),
            ["pins"] = Clean(sensor.Pins, 64),
            ["status"] = status,
            ["detail"] = Clean(sensor.Detail, 200),
            ["value"] = NormalizeValue(sensor.Value)
        };
    }

    private static object? NormalizeValue(JsonElement? value)
    {
        if (value is not { } element)
            return null;

        return element.ValueKind switch
        {
            JsonValueKind.String => Clean(element.GetString(), 128),
            JsonValueKind.Number when element.TryGetInt64(out var integer) => integer,
            JsonValueKind.Number when element.TryGetDouble(out var number) && double.IsFinite(number) => number,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private static bool ReadCapability(
        IReadOnlyDictionary<string, JsonElement>? capabilities,
        string key)
    {
        if (capabilities is null || !capabilities.TryGetValue(key, out var value))
            return false;

        return value.ValueKind == JsonValueKind.True;
    }

    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
