using System.Text.Json;
using AuthUtility.Api.IoT;
using AuthUtility.Application.DTOs.IoT;

namespace AuthUtility.Tests;

public class IotDiscoveryRegistryTests
{
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan duration) => _now += duration;
    }

    private static IotDeviceDiscoveryRequest Request(string deviceId = "dl-test-01") => new()
    {
        DeviceId = deviceId,
        DeviceType = "DATALOGGER",
        Platform = "ESP32",
        BoardType = "esp32-devkit-v1",
        FirmwareVersion = "1.1.3",
        IpAddress = "10.0.0.10",
        Rssi = -55,
        MacAddress = "AA:BB:CC:DD:EE:FF",
        ChipId = "001122334455",
        Capabilities = new Dictionary<string, JsonElement>
        {
            ["rtcDs3231"] = JsonDocument.Parse("true").RootElement.Clone(),
            ["zhafiraRainGauge"] = JsonDocument.Parse("false").RootElement.Clone()
        },
        Sensors =
        [
            new IotHardwareSensorDto
            {
                Key = "DS3231",
                Label = "RTC DS3231",
                Interface = "I2C",
                Pins = "SDA GPIO21 / SCL GPIO22",
                Status = "responsive",
                Detail = "Waktu valid.",
                Value = JsonDocument.Parse("\"2026-11-19T10:00:00\"").RootElement.Clone()
            }
        ]
    };

    [Fact]
    public void BeaconValidMasukSnapshotDenganIdNormal()
    {
        var time = new FixedTimeProvider(new DateTimeOffset(2026, 11, 19, 10, 0, 0, TimeSpan.Zero));
        var registry = new IotDiscoveryRegistry(time);

        var result = registry.Announce(Request(" dl-test-01 "), "10.0.0.10");

        Assert.Equal(IotDiscoveryResultKind.Accepted, result.Kind);
        var snapshot = Assert.Single(registry.Snapshot());
        Assert.Equal("DL-TEST-01", snapshot.DeviceId);
        Assert.Equal("1.1.3", snapshot.FirmwareVersion);
        Assert.Equal(JsonValueKind.Object, snapshot.Capabilities.ValueKind);
    }

    [Fact]
    public void BeaconBerulangDalamLimaDetikDibatasi()
    {
        var time = new FixedTimeProvider(new DateTimeOffset(2026, 11, 19, 10, 0, 0, TimeSpan.Zero));
        var registry = new IotDiscoveryRegistry(time);

        registry.Announce(Request(), null);
        var second = registry.Announce(Request(), null);

        Assert.Equal(IotDiscoveryResultKind.Throttled, second.Kind);
    }

    [Fact]
    public void BeaconKedaluwarsaSetelahTigaPuluhMenit()
    {
        var time = new FixedTimeProvider(new DateTimeOffset(2026, 11, 19, 10, 0, 0, TimeSpan.Zero));
        var registry = new IotDiscoveryRegistry(time);
        registry.Announce(Request(), null);

        time.Advance(TimeSpan.FromMinutes(30));

        Assert.Empty(registry.Snapshot());
    }

    [Fact]
    public void DeviceIdTidakValidDitolak()
    {
        var time = new FixedTimeProvider(new DateTimeOffset(2026, 11, 19, 10, 0, 0, TimeSpan.Zero));
        var registry = new IotDiscoveryRegistry(time);

        var result = registry.Announce(Request("bad id/../../x"), null);

        Assert.Equal(IotDiscoveryResultKind.Invalid, result.Kind);
    }

    [Fact]
    public void ProvisioningDapatMenghapusEntry()
    {
        var time = new FixedTimeProvider(new DateTimeOffset(2026, 11, 19, 10, 0, 0, TimeSpan.Zero));
        var registry = new IotDiscoveryRegistry(time);
        registry.Announce(Request(), null);

        Assert.True(registry.Remove("dl-test-01"));
        Assert.Empty(registry.Snapshot());
    }

    [Fact]
    public void SnapshotMembatasiSensorDanMenormalkanStatus()
    {
        var sensors = Enumerable.Range(0, 20)
            .Select(index => new IotHardwareSensorDto
            {
                Key = $"S-{index}",
                Label = new string('x', 100),
                Interface = "I2C",
                Pins = "GPIO1",
                Status = "connected",
                Detail = new string('y', 300)
            })
            .ToList();

        var json = IotHardwareSnapshot.CreateJson(
            null,
            null,
            null,
            null,
            "DATALOGGER",
            "ESP32",
            "esp32-devkit-v1",
            null,
            "1.1.3",
            null,
            null,
            null,
            null,
            sensors);

        using var document = JsonDocument.Parse(json);
        var sensorArray = document.RootElement.GetProperty("sensors");
        Assert.Equal(16, sensorArray.GetArrayLength());
        Assert.Equal("unverified", sensorArray[0].GetProperty("status").GetString());
        Assert.Equal(64, sensorArray[0].GetProperty("label").GetString()!.Length);
        Assert.Equal(200, sensorArray[0].GetProperty("detail").GetString()!.Length);
    }
}
