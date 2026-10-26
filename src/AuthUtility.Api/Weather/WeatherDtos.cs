namespace AuthUtility.Api.Weather;

public sealed record WeatherQuery(
    double Latitude,
    double Longitude,
    int Days = 10,
    string? Area = null);

public sealed class WeatherQueryException(string message) : ArgumentException(message);

public sealed class WeatherForecastResponse
{
    public WeatherLocation Location { get; init; } = new();
    public DateTimeOffset FetchedAt { get; init; }
    public string ForecastSource { get; init; } = "Open-Meteo";
    public string AlertSource { get; init; } = "BMKG";
    public string AlertStatus { get; init; } = "unavailable";
    public IReadOnlyList<WeatherDayForecast> Forecast { get; init; } = [];
    public IReadOnlyList<WeatherAlert> Alerts { get; init; } = [];
}

public sealed class WeatherLocation
{
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string Timezone { get; init; } = "auto";
}

public sealed class WeatherDayForecast
{
    public string Date { get; init; } = string.Empty;
    public int? WeatherCode { get; init; }
    public string Summary { get; init; } = "Kondisi tidak diketahui";
    public double? TemperatureMaxC { get; init; }
    public double? TemperatureMinC { get; init; }
    public int? PrecipitationProbability { get; init; }
    public double? PrecipitationMm { get; init; }
    public double? WindMaxKmh { get; init; }
}

public sealed class WeatherAlert
{
    public string Title { get; init; } = "Peringatan dini cuaca";
    public string? Description { get; init; }
    public string? Effective { get; init; }
    public string? Expires { get; init; }
    public string? Area { get; init; }
    public string? Severity { get; init; }
    public string? Urgency { get; init; }
    public string? Certainty { get; init; }
    public string? Url { get; init; }
}
