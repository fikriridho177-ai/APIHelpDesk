namespace AuthUtility.Api.IoT;

public static class IotMonitoringRules
{
    public static bool HasValidCoordinates(
        double? latitude,
        double? longitude)
    {
        return latitude.HasValue
            && longitude.HasValue
            && double.IsFinite(latitude.Value)
            && double.IsFinite(longitude.Value)
            && latitude.Value is >= -90 and <= 90
            && longitude.Value is >= -180 and <= 180;
    }

    public static string DetermineStatus(
        bool aktif,
        int interval,
        double? ageMinutes)
    {
        if (!aktif)
            return "INACTIVE";

        if (ageMinutes is null)
            return "NO_RECENT_DATA";

        if (!double.IsFinite(ageMinutes.Value) || interval <= 0)
            return "UNKNOWN";

        if (ageMinutes <= interval * 2.0)
            return "ONLINE";

        if (ageMinutes <= interval * 6.0)
            return "STALE";

        return "NO_RECENT_DATA";
    }
}
