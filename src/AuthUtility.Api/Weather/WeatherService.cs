using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace AuthUtility.Api.Weather;

public sealed class WeatherService(
    HttpClient httpClient,
    IOptions<WeatherOptions> options,
    IMemoryCache cache,
    ILogger<WeatherService> logger)
{
    private readonly HttpClient httpClient = httpClient;
    private readonly WeatherOptions options = options.Value;
    private readonly IMemoryCache cache = cache;
    private readonly ILogger<WeatherService> logger = logger;

    public async Task<WeatherForecastResponse> GetForecastAsync(
        WeatherQuery query,
        CancellationToken cancellationToken)
    {
        Validate(query);

        var area = NormalizeArea(query.Area);
        var normalizedQuery = query with { Area = area };
        var cacheKey = BuildCacheKey(normalizedQuery);

        if (cache.TryGetValue(cacheKey, out WeatherForecastResponse? cached)
            && cached is not null)
        {
            return cached;
        }

        var forecast = await FetchForecastAsync(
            normalizedQuery,
            cancellationToken);

        var (alertStatus, alerts) = await FetchAlertsAsync(
            normalizedQuery,
            cancellationToken);

        var result = new WeatherForecastResponse
        {
            Location = new WeatherLocation
            {
                Latitude = normalizedQuery.Latitude,
                Longitude = normalizedQuery.Longitude,
                Timezone = forecast.Timezone
            },
            FetchedAt = DateTimeOffset.UtcNow,
            ForecastSource = "Open-Meteo",
            AlertSource = "BMKG",
            AlertStatus = alertStatus,
            Forecast = forecast.Days,
            Alerts = alerts
        };

        cache.Set(
            cacheKey,
            result,
            TimeSpan.FromMinutes(Math.Clamp(options.CacheMinutes, 1, 60)));

        return result;
    }

    private async Task<(string Timezone, IReadOnlyList<WeatherDayForecast> Days)>
        FetchForecastAsync(
            WeatherQuery query,
            CancellationToken cancellationToken)
    {
        var url = BuildOpenMeteoUrl(query);
        using var response = await httpClient.GetAsync(
            url,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var contentStream = await response.Content.ReadAsStreamAsync(
            cancellationToken);
        using var document = await JsonDocument.ParseAsync(
            contentStream,
            cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("daily", out var daily))
            throw new HttpRequestException("Open-Meteo tidak mengembalikan data harian.");

        var dates = ReadStringArray(daily, "time");
        var days = dates
            .Select((date, index) => new WeatherDayForecast
            {
                Date = date,
                WeatherCode = ReadInt(daily, "weather_code", index),
                Summary = GetWeatherSummary(
                    ReadInt(daily, "weather_code", index)),
                TemperatureMaxC = ReadDouble(
                    daily,
                    "temperature_2m_max",
                    index),
                TemperatureMinC = ReadDouble(
                    daily,
                    "temperature_2m_min",
                    index),
                PrecipitationProbability = ReadInt(
                    daily,
                    "precipitation_probability_max",
                    index),
                PrecipitationMm = ReadDouble(
                    daily,
                    "precipitation_sum",
                    index),
                WindMaxKmh = ReadDouble(
                    daily,
                    "wind_speed_10m_max",
                    index)
            })
            .ToArray();

        var timezone = document.RootElement.TryGetProperty(
            "timezone",
            out var timezoneElement)
            ? timezoneElement.GetString() ?? "auto"
            : "auto";

        return (timezone, days);
    }

    private async Task<(string Status, IReadOnlyList<WeatherAlert> Alerts)>
        FetchAlertsAsync(
            WeatherQuery query,
            CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync(
                options.BmkgAlertUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            response.EnsureSuccessStatusCode();
            var xml = await response.Content.ReadAsStringAsync(
                cancellationToken);
            var alerts = ParseBmkgAlerts(xml)
                .Where(alert => IsRelevant(alert, query.Area))
                .Take(20)
                .ToArray();

            return ("available", alerts);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Request peringatan BMKG timeout.");
            return ("unavailable", []);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Feed peringatan BMKG tidak tersedia.");
            return ("unavailable", []);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning(exception, "Format feed peringatan BMKG tidak valid.");
            return ("unavailable", []);
        }
        catch (XmlException exception)
        {
            logger.LogWarning(exception, "XML feed peringatan BMKG tidak valid.");
            return ("unavailable", []);
        }
    }

    public static IReadOnlyList<WeatherAlert> ParseBmkgAlerts(string xml)
    {
        var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        var entries = document
            .Descendants()
            .Where(element =>
                element.Name.LocalName is "item" or "info")
            .ToList();

        if (entries.Count == 0 && document.Root?.Name.LocalName == "alert")
            entries.Add(document.Root);

        return entries
            .Select(ParseAlert)
            .Where(alert => alert is not null)
            .Cast<WeatherAlert>()
            .ToArray();
    }

    private static WeatherAlert? ParseAlert(XElement entry)
    {
        var title = ReadXmlValue(entry, "title")
            ?? ReadXmlValue(entry, "headline")
            ?? ReadXmlValue(entry, "event");
        var description = ReadXmlValue(entry, "description")
            ?? ReadXmlValue(entry, "instruction");

        if (string.IsNullOrWhiteSpace(title)
            && string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        return new WeatherAlert
        {
            Title = title?.Trim() ?? "Peringatan dini cuaca",
            Description = TrimOrNull(description),
            Effective = TrimOrNull(ReadXmlValue(entry, "effective")),
            Expires = TrimOrNull(ReadXmlValue(entry, "expires")),
            Area = TrimOrNull(
                ReadXmlValue(entry, "area")
                ?? ReadXmlValue(entry, "areaDesc")),
            Severity = TrimOrNull(ReadXmlValue(entry, "severity")),
            Urgency = TrimOrNull(ReadXmlValue(entry, "urgency")),
            Certainty = TrimOrNull(ReadXmlValue(entry, "certainty")),
            Url = TrimOrNull(
                ReadXmlValue(entry, "link")
                ?? ReadXmlValue(entry, "web"))
        };
    }

    private static bool IsRelevant(WeatherAlert alert, string? area)
    {
        if (string.IsNullOrWhiteSpace(area))
            return true;

        var search = area.Trim();
        return string.Join(
                " ",
                alert.Area,
                alert.Title,
                alert.Description)
            .Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private string BuildOpenMeteoUrl(WeatherQuery query)
    {
        var baseUrl = options.OpenMeteoBaseUrl.TrimEnd('/');
        var latitude = query.Latitude.ToString("0.######", CultureInfo.InvariantCulture);
        var longitude = query.Longitude.ToString("0.######", CultureInfo.InvariantCulture);

        return $"{baseUrl}/v1/forecast"
            + $"?latitude={latitude}"
            + $"&longitude={longitude}"
            + "&daily=weather_code,temperature_2m_max,temperature_2m_min,"
            + "precipitation_probability_max,precipitation_sum,wind_speed_10m_max"
            + "&timezone=auto"
            + $"&forecast_days={query.Days}";
    }

    private static string BuildCacheKey(WeatherQuery query) =>
        $"weather:v1:{query.Latitude.ToString("0.######", CultureInfo.InvariantCulture)}:"
        + $"{query.Longitude.ToString("0.######", CultureInfo.InvariantCulture)}:"
        + $"{query.Days}:{query.Area?.ToUpperInvariant() ?? "*"}";

    private static void Validate(WeatherQuery query)
    {
        if (double.IsNaN(query.Latitude)
            || double.IsInfinity(query.Latitude)
            || query.Latitude is < -90 or > 90)
        {
            throw new WeatherQueryException(
                "Latitude harus berada di antara -90 dan 90.");
        }

        if (double.IsNaN(query.Longitude)
            || double.IsInfinity(query.Longitude)
            || query.Longitude is < -180 or > 180)
        {
            throw new WeatherQueryException(
                "Longitude harus berada di antara -180 dan 180.");
        }

        if (query.Days is < 1 or > 10)
        {
            throw new WeatherQueryException(
                "Jumlah hari prakiraan harus berada di antara 1 dan 10.");
        }
    }

    private static string? NormalizeArea(string? area) =>
        string.IsNullOrWhiteSpace(area) ? null : area.Trim();

    private static string? ReadXmlValue(XElement parent, string localName) =>
        parent
            .DescendantsAndSelf()
            .FirstOrDefault(element => element.Name.LocalName == localName)
            ?.Value;

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string[] ReadStringArray(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var property)
            || property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return property
            .EnumerateArray()
            .Select(element => element.GetString() ?? string.Empty)
            .ToArray();
    }

    private static int? ReadInt(JsonElement parent, string name, int index)
    {
        if (!parent.TryGetProperty(name, out var property)
            || property.ValueKind != JsonValueKind.Array
            || index >= property.GetArrayLength()
            || !property[index].TryGetInt32(out var value))
        {
            return null;
        }

        return value;
    }

    private static double? ReadDouble(JsonElement parent, string name, int index)
    {
        if (!parent.TryGetProperty(name, out var property)
            || property.ValueKind != JsonValueKind.Array
            || index >= property.GetArrayLength()
            || !property[index].TryGetDouble(out var value))
        {
            return null;
        }

        return value;
    }

    private static string GetWeatherSummary(int? code) => code switch
    {
        0 => "Cerah",
        1 => "Cerah berawan",
        2 => "Berawan sebagian",
        3 => "Mendung",
        45 or 48 => "Kabut",
        51 or 53 or 55 => "Gerimis",
        56 or 57 => "Gerimis beku",
        61 => "Hujan ringan",
        63 => "Hujan sedang",
        65 => "Hujan lebat",
        66 or 67 => "Hujan beku",
        71 or 73 or 75 or 77 or 85 or 86 => "Salju",
        80 => "Hujan lokal ringan",
        81 => "Hujan lokal sedang",
        82 => "Hujan lokal lebat",
        95 => "Badai petir",
        96 or 99 => "Badai petir dan hujan es",
        _ => "Kondisi tidak diketahui"
    };
}
