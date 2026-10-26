using System.Net;
using System.Text;
using AuthUtility.Api.Weather;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AuthUtility.Tests;

public sealed class WeatherServiceTests
{
    [Fact]
    public async Task GetForecastAsync_maps_ten_days_and_bmkg_alerts()
    {
        var requests = new List<Uri>();
        var service = CreateService(request =>
        {
            requests.Add(request.RequestUri!);

            if (request.RequestUri!.Host == "open-meteo.test")
                return JsonResponse(OpenMeteoResponse());

            return XmlResponse(BmkgAlertResponse());
        });

        var result = await service.GetForecastAsync(
            new WeatherQuery(-0.5071, 101.4478, 10, "Riau"),
            CancellationToken.None);

        Assert.Equal(10, result.Forecast.Count);
        Assert.Equal("2026-10-26", result.Forecast[0].Date);
        Assert.Equal("Hujan ringan", result.Forecast[1].Summary);
        Assert.Equal(70, result.Forecast[1].PrecipitationProbability);
        Assert.Equal("available", result.AlertStatus);
        var alert = Assert.Single(result.Alerts);
        Assert.Equal("Riau", alert.Area);
        Assert.Equal("Waspada hujan lebat", alert.Title);
        Assert.Contains(requests, uri => uri.Host == "open-meteo.test");
        Assert.Contains(requests, uri => uri.Host == "bmkg.test");
    }

    [Fact]
    public async Task GetForecastAsync_rejects_invalid_coordinates_before_http_call()
    {
        var requestCount = 0;
        var service = CreateService(request =>
        {
            requestCount++;
            return JsonResponse(OpenMeteoResponse());
        });

        await Assert.ThrowsAsync<WeatherQueryException>(() =>
            service.GetForecastAsync(
                new WeatherQuery(-91, 101.4478),
                CancellationToken.None));

        Assert.Equal(0, requestCount);
    }

    [Fact]
    public async Task GetForecastAsync_keeps_forecast_when_bmkg_is_unavailable()
    {
        var service = CreateService(request =>
        {
            if (request.RequestUri!.Host == "open-meteo.test")
                return JsonResponse(OpenMeteoResponse());

            throw new HttpRequestException("BMKG unavailable");
        });

        var result = await service.GetForecastAsync(
            new WeatherQuery(-0.5071, 101.4478),
            CancellationToken.None);

        Assert.NotEmpty(result.Forecast);
        Assert.Empty(result.Alerts);
        Assert.Equal("unavailable", result.AlertStatus);
    }

    [Fact]
    public async Task GetForecastAsync_keeps_forecast_when_bmkg_xml_is_malformed()
    {
        var service = CreateService(request =>
        {
            if (request.RequestUri!.Host == "open-meteo.test")
                return JsonResponse(OpenMeteoResponse());

            return XmlResponse("<rss><channel><item><title>broken");
        });

        var result = await service.GetForecastAsync(
            new WeatherQuery(-0.5071, 101.4478),
            CancellationToken.None);

        Assert.NotEmpty(result.Forecast);
        Assert.Empty(result.Alerts);
        Assert.Equal("unavailable", result.AlertStatus);
    }

    [Fact]
    public async Task GetForecastAsync_raises_provider_error_when_open_meteo_payload_is_malformed()
    {
        var service = CreateService(request =>
        {
            if (request.RequestUri!.Host == "open-meteo.test")
                return JsonResponse("{ malformed");

            return XmlResponse("<rss />");
        });

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.GetForecastAsync(
                new WeatherQuery(-0.5071, 101.4478),
                CancellationToken.None));
    }

    [Fact]
    public async Task GetForecastAsync_raises_provider_error_when_open_meteo_times_out()
    {
        var service = CreateService(_ => throw new TaskCanceledException("Open-Meteo timeout"));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.GetForecastAsync(
                new WeatherQuery(-0.5071, 101.4478),
                CancellationToken.None));
    }

    private static WeatherService CreateService(
        Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var client = new HttpClient(new StubHttpMessageHandler(handler));
        var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new WeatherOptions
        {
            OpenMeteoBaseUrl = "https://open-meteo.test",
            BmkgAlertUrl = "https://bmkg.test/alerts/nowcast/id",
            CacheMinutes = 15
        });

        return new WeatherService(
            client,
            options,
            cache,
            NullLogger<WeatherService>.Instance);
    }

    private static HttpResponseMessage JsonResponse(string content) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                content,
                Encoding.UTF8,
                "application/json")
        };

    private static HttpResponseMessage XmlResponse(string content) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                content,
                Encoding.UTF8,
                "application/rss+xml")
        };

    private static string OpenMeteoResponse() => """
        {
          "timezone": "Asia/Jakarta",
          "daily": {
            "time": ["2026-10-26", "2026-10-27", "2026-10-28", "2026-10-29", "2026-10-30", "2026-10-31", "2026-11-01", "2026-11-02", "2026-11-03", "2026-11-04"],
            "weather_code": [3, 61, 63, 80, 95, 2, 1, 3, 61, 0],
            "temperature_2m_max": [30.2, 30.4, 29.8, 30.1, 29.5, 31.0, 31.2, 30.7, 29.9, 31.4],
            "temperature_2m_min": [23.8, 24.0, 23.7, 23.9, 23.5, 24.1, 24.0, 23.9, 23.6, 24.2],
            "precipitation_probability_max": [20, 70, 80, 65, 90, 20, 10, 30, 75, 5],
            "precipitation_sum": [0.0, 8.4, 12.1, 6.5, 24.0, 0.0, 0.0, 1.2, 9.0, 0.0],
            "wind_speed_10m_max": [12.0, 15.5, 14.1, 18.3, 21.0, 10.4, 11.5, 13.2, 16.0, 9.1]
          }
        }
        """;

    private static string BmkgAlertResponse() => """
        <?xml version="1.0" encoding="UTF-8"?>
        <rss xmlns="https://bmkg.go.id/alerts/nowcast">
          <channel>
            <item>
              <title>Waspada hujan lebat</title>
              <description>Hujan lebat berpotensi terjadi di beberapa wilayah.</description>
              <area>Riau</area>
              <severity>Moderate</severity>
              <urgency>Expected</urgency>
              <certainty>Likely</certainty>
              <effective>2026-10-26T09:00:00+07:00</effective>
              <expires>2026-10-26T12:00:00+07:00</expires>
              <link>https://www.bmkg.go.id/alerts/nowcast/id/riau</link>
            </item>
          </channel>
        </rss>
        """;

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(handler(request));
    }
}
