using System.Text.Json;
using AuthUtility.Application.DTOs.IoT;

namespace AuthUtility.Api.IoT;

public enum IotDiscoveryResultKind
{
    Accepted,
    Throttled,
    Invalid,
    RateLimited
}

public sealed record IotDiscoveryResult(
    IotDiscoveryResultKind Kind,
    IotDiscoveredDevice? Device = null);

public sealed record IotDiscoveredDevice
{
    public string DeviceId { get; init; } = "";
    public string DeviceType { get; init; } = "";
    public string Platform { get; init; } = "";
    public string BoardType { get; init; } = "";
    public string BoardRevision { get; init; } = "";
    public string FirmwareVersion { get; init; } = "";
    public string? IpAddress { get; init; }
    public int? Rssi { get; init; }
    public long? UptimeSeconds { get; init; }
    public string? MacAddress { get; init; }
    public string? ChipId { get; init; }
    public string? ChipModel { get; init; }
    public int? ChipRevision { get; init; }
    public JsonElement Capabilities { get; init; }
    public DateTimeOffset FirstSeenAt { get; init; }
    public DateTimeOffset LastSeenAt { get; init; }
    public string? SourceIp { get; init; }
}

/// <summary>
/// Registry beacon perangkat yang belum dapat memakai credential API.
/// Disimpan di memori dengan TTL agar endpoint anonim tidak dapat menulis DB.
/// Perangkat akan mengirim beacon ulang setelah API restart.
/// </summary>
public sealed class IotDiscoveryRegistry
{
    private static readonly TimeSpan EntryTtl = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RateWindow = TimeSpan.FromMinutes(1);
    private const int MaxEntries = 256;
    private const int MaxTrackedIps = 1024;
    private const int MaxAnnouncementsPerIp = 120;

    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<string, IotDiscoveredDevice> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Queue<DateTimeOffset>> _ipHits = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public IotDiscoveryRegistry(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public IotDiscoveryResult Announce(IotDeviceDiscoveryRequest request, string? sourceIp)
    {
        var deviceId = NormalizeDeviceId(request.DeviceId);
        if (deviceId is null)
            return new IotDiscoveryResult(IotDiscoveryResultKind.Invalid);

        var now = _timeProvider.GetUtcNow();
        var snapshotJson = IotHardwareSnapshot.CreateJson(
            request.MacAddress,
            request.ChipId,
            request.ChipModel,
            request.ChipRevision,
            request.DeviceType,
            request.Platform,
            request.BoardType,
            request.BoardRevision,
            request.FirmwareVersion,
            request.IpAddress,
            request.Rssi,
            request.UptimeSeconds,
            request.Capabilities,
            request.Sensors);

        using var document = JsonDocument.Parse(snapshotJson);

        lock (_gate)
        {
            PurgeExpired(now);

            if (!AllowSourceIp(sourceIp, now))
                return new IotDiscoveryResult(IotDiscoveryResultKind.RateLimited);

            if (_entries.TryGetValue(deviceId, out var existing))
            {
                if (now - existing.LastSeenAt < UpdateInterval)
                    return new IotDiscoveryResult(IotDiscoveryResultKind.Throttled, existing);

                var updated = CreateEntry(
                    deviceId,
                    request,
                    sourceIp,
                    document.RootElement,
                    existing.FirstSeenAt,
                    now);
                _entries[deviceId] = updated;
                return new IotDiscoveryResult(IotDiscoveryResultKind.Accepted, updated);
            }

            if (_entries.Count >= MaxEntries)
                EvictOldest();

            var created = CreateEntry(
                deviceId,
                request,
                sourceIp,
                document.RootElement,
                now,
                now);
            _entries[deviceId] = created;
            return new IotDiscoveryResult(IotDiscoveryResultKind.Accepted, created);
        }
    }

    public IReadOnlyList<IotDiscoveredDevice> Snapshot()
    {
        var now = _timeProvider.GetUtcNow();

        lock (_gate)
        {
            PurgeExpired(now);
            return _entries.Values
                .OrderByDescending(entry => entry.LastSeenAt)
                .ToList();
        }
    }

    public IotDiscoveredDevice? Find(string deviceId)
    {
        var normalized = NormalizeDeviceId(deviceId);
        if (normalized is null)
            return null;

        lock (_gate)
        {
            PurgeExpired(_timeProvider.GetUtcNow());
            return _entries.TryGetValue(normalized, out var entry) ? entry : null;
        }
    }

    public bool Remove(string deviceId)
    {
        var normalized = NormalizeDeviceId(deviceId);
        if (normalized is null)
            return false;

        lock (_gate)
        {
            return _entries.Remove(normalized);
        }
    }

    private static IotDiscoveredDevice CreateEntry(
        string deviceId,
        IotDeviceDiscoveryRequest request,
        string? sourceIp,
        JsonElement capabilities,
        DateTimeOffset firstSeenAt,
        DateTimeOffset lastSeenAt)
    {
        return new IotDiscoveredDevice
        {
            DeviceId = deviceId,
            DeviceType = Clean(request.DeviceType, 64) ?? "",
            Platform = Clean(request.Platform, 64) ?? "",
            BoardType = Clean(request.BoardType, 64) ?? "",
            BoardRevision = Clean(request.BoardRevision, 64) ?? "",
            FirmwareVersion = Clean(request.FirmwareVersion, 64) ?? "",
            IpAddress = Clean(request.IpAddress, 64),
            Rssi = request.Rssi,
            UptimeSeconds = request.UptimeSeconds,
            MacAddress = Clean(request.MacAddress, 64),
            ChipId = Clean(request.ChipId, 64),
            ChipModel = Clean(request.ChipModel, 64),
            ChipRevision = request.ChipRevision,
            Capabilities = capabilities.Clone(),
            FirstSeenAt = firstSeenAt,
            LastSeenAt = lastSeenAt,
            SourceIp = Clean(sourceIp, 64)
        };
    }

    private bool AllowSourceIp(string? sourceIp, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(sourceIp))
            return true;

        if (!_ipHits.TryGetValue(sourceIp, out var hits))
        {
            if (_ipHits.Count >= MaxTrackedIps)
                return false;

            hits = new Queue<DateTimeOffset>();
            _ipHits[sourceIp] = hits;
        }

        while (hits.Count > 0 && now - hits.Peek() >= RateWindow)
            hits.Dequeue();

        if (hits.Count >= MaxAnnouncementsPerIp)
            return false;

        hits.Enqueue(now);
        return true;
    }

    private void PurgeExpired(DateTimeOffset now)
    {
        foreach (var key in _entries
                     .Where(pair => now - pair.Value.LastSeenAt >= EntryTtl)
                     .Select(pair => pair.Key)
                     .ToList())
        {
            _entries.Remove(key);
        }

        foreach (var key in _ipHits
                     .Where(pair =>
                     {
                         while (pair.Value.Count > 0 && now - pair.Value.Peek() >= RateWindow)
                             pair.Value.Dequeue();
                         return pair.Value.Count == 0;
                     })
                     .Select(pair => pair.Key)
                     .ToList())
        {
            _ipHits.Remove(key);
        }
    }

    private void EvictOldest()
    {
        var oldest = _entries
            .OrderBy(pair => pair.Value.LastSeenAt)
            .FirstOrDefault();
        if (!string.IsNullOrEmpty(oldest.Key))
            _entries.Remove(oldest.Key);
    }

    private static string? NormalizeDeviceId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim().ToUpperInvariant();
        if (normalized.Length is < 1 or > 64)
            return null;

        return normalized.All(character =>
            char.IsAsciiLetterOrDigit(character)
            || character is '-' or '_' or '.')
            ? normalized
            : null;
    }

    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
