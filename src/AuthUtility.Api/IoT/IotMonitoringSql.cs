namespace AuthUtility.Api.IoT;

public static class IotMonitoringSql
{
    // Device health is based on when the API accepted the payload. Sensor
    // clocks can be stale or arrive out of order.
    public const string LatestPayloadOrderBy =
        "RH.ReceivedAt DESC, RH.RawID DESC";
}
