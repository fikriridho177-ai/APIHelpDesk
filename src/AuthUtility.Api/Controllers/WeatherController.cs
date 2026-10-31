using AuthUtility.Api.Weather;
using AuthUtility.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthUtility.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/weather")]
public sealed class WeatherController(
    WeatherService weather,
    IPermissionService permissions,
    ILogger<WeatherController> logger) : ControllerBase
{
    private const string MenuCode = "DASHBOARD_IOT";

    [HttpGet("forecast")]
    public async Task<IActionResult> GetForecast(
        [FromQuery] double? latitude,
        [FromQuery] double? longitude,
        [FromQuery] int days = 10,
        [FromQuery] string? area = null,
        CancellationToken cancellationToken = default)
    {
        var permission = await permissions.GetPermissionsAsync(User, MenuCode);
        if (permission?.IsSuperAdmin != true)
            return Forbid();

        if (latitude is null || longitude is null)
        {
            return BadRequest(new
            {
                message = "Latitude dan longitude wajib diisi."
            });
        }

        try
        {
            var result = await weather.GetForecastAsync(
                new WeatherQuery(latitude.Value, longitude.Value, days, area),
                cancellationToken);

            return Ok(result);
        }
        catch (WeatherQueryException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Provider prakiraan cuaca gagal.");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    message = "Provider prakiraan cuaca sedang tidak tersedia."
                });
        }
    }
}
