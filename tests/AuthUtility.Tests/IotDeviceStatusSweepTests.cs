using AuthUtility.Api.IoT;
using AuthUtility.Domain.Entities;

namespace AuthUtility.Tests;

public class IotDeviceStatusSweepTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 10, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan OfflineAfter = TimeSpan.FromMinutes(30);

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void OnlineDenganHeartbeatBaruTetapOnline(int minutes)
    {
        var next = IotDeviceStatusSweepService.NextStatus(
            IotDeviceStatus.Online,
            Now.AddMinutes(-minutes),
            Now,
            StaleAfter,
            OfflineAfter);

        Assert.Null(next);
    }

    [Fact]
    public void OnlineTanpaHeartbeatLimaMenitMenjadiStale()
    {
        var next = IotDeviceStatusSweepService.NextStatus(
            IotDeviceStatus.Online,
            Now.AddMinutes(-5),
            Now,
            StaleAfter,
            OfflineAfter);

        Assert.Equal(IotDeviceStatus.Stale, next);
    }

    [Fact]
    public void OnlineYangSudahLamaLangsungMenjadiStaleBukanOffline()
    {
        var next = IotDeviceStatusSweepService.NextStatus(
            IotDeviceStatus.Online,
            Now.AddMinutes(-45),
            Now,
            StaleAfter,
            OfflineAfter);

        Assert.Equal(IotDeviceStatus.Stale, next);
    }

    [Fact]
    public void StaleTanpaHeartbeatTigaPuluhMenitMenjadiOffline()
    {
        var next = IotDeviceStatusSweepService.NextStatus(
            IotDeviceStatus.Stale,
            Now.AddMinutes(-30),
            Now,
            StaleAfter,
            OfflineAfter);

        Assert.Equal(IotDeviceStatus.Offline, next);
    }

    [Fact]
    public void StaleDenganHeartbeatBaruDibiarkanUntukHeartbeatBerikutnya()
    {
        var next = IotDeviceStatusSweepService.NextStatus(
            IotDeviceStatus.Stale,
            Now.AddSeconds(-30),
            Now,
            StaleAfter,
            OfflineAfter);

        Assert.Null(next);
    }

    [Fact]
    public void DisabledTidakPernahDiubah()
    {
        var next = IotDeviceStatusSweepService.NextStatus(
            IotDeviceStatus.Disabled,
            null,
            Now,
            StaleAfter,
            OfflineAfter);

        Assert.Null(next);
    }

    [Fact]
    public void OnlineTanpaLastSeenMenjadiOffline()
    {
        var next = IotDeviceStatusSweepService.NextStatus(
            IotDeviceStatus.Online,
            null,
            Now,
            StaleAfter,
            OfflineAfter);

        Assert.Equal(IotDeviceStatus.Offline, next);
    }

    [Fact]
    public void DefaultThresholdSesuaiKebutuhan()
    {
        var options = new IotDeviceStatusSweepOptions();

        Assert.Equal(30, options.IntervalSeconds);
        Assert.Equal(300, options.StaleAfterSeconds);
        Assert.Equal(1800, options.OfflineAfterSeconds);
    }
}
