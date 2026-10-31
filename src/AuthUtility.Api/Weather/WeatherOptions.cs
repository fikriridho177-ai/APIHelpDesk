namespace AuthUtility.Api.Weather;

public sealed class WeatherOptions
{
    public string OpenMeteoBaseUrl { get; set; } = "https://api.open-meteo.com";
    public string BmkgAlertUrl { get; set; } = "https://www.bmkg.go.id/alerts/nowcast/id";
    public int CacheMinutes { get; set; } = 15;
    public int TimeoutSeconds { get; set; } = 12;
}
