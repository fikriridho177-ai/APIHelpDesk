using AuthUtility.Api.IoT;

namespace AuthUtility.Tests;

public sealed class IotMonitoringControllerTests
{
    [Fact]
    public void Coordinates_are_valid_only_when_both_values_are_finite_and_in_range()
    {
        Assert.True(IotMonitoringRules.HasValidCoordinates(0.3638889, 103.4896549));
        Assert.False(IotMonitoringRules.HasValidCoordinates(null, 103.4896549));
        Assert.False(IotMonitoringRules.HasValidCoordinates(91, 103.4896549));
        Assert.False(IotMonitoringRules.HasValidCoordinates(0.3638889, double.NaN));
    }

    [Fact]
    public void Invalid_interval_is_unknown_and_never_online()
    {
        Assert.Equal("UNKNOWN", IotMonitoringRules.DetermineStatus(true, 0, 1));
        Assert.Equal("UNKNOWN", IotMonitoringRules.DetermineStatus(true, 5, double.PositiveInfinity));
        Assert.Equal("ONLINE", IotMonitoringRules.DetermineStatus(true, 5, 10));
        Assert.Equal("INACTIVE", IotMonitoringRules.DetermineStatus(false, 5, 1));
    }

    [Fact]
    public void Latest_payload_order_uses_server_receive_time()
    {
        Assert.Equal(
            "RH.ReceivedAt DESC, RH.RawID DESC",
            IotMonitoringSql.LatestPayloadOrderBy);
    }
}
