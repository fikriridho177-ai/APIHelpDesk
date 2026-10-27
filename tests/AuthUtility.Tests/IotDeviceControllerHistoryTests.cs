using AuthUtility.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace AuthUtility.Tests;

public sealed class IotDeviceControllerHistoryTests
{
    [Fact]
    public async Task GetHistory_rejects_partial_custom_range_before_database_access()
    {
        var controller = new IotDeviceController(
            null!,
            null!,
            NullLogger<IotDeviceController>.Instance);

        var result = await controller.GetHistory(
            7,
            period: "week",
            fromValue: "2026-10-20T00:00");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("from dan to", badRequest.Value?.ToString());
    }

    [Fact]
    public async Task GetHistory_rejects_reversed_custom_range_before_database_access()
    {
        var controller = new IotDeviceController(
            null!,
            null!,
            NullLogger<IotDeviceController>.Instance);

        var result = await controller.GetHistory(
            7,
            period: "month",
            fromValue: "2026-10-27T00:00",
            toValue: "2026-10-20T00:00");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("lebih kecil", badRequest.Value?.ToString());
    }

    [Fact]
    public async Task GetHistory_rejects_malformed_custom_range_before_database_access()
    {
        var controller = new IotDeviceController(
            null!,
            null!,
            NullLogger<IotDeviceController>.Instance);

        var result = await controller.GetHistory(
            7,
            period: "year",
            fromValue: "2026-10-20 00:00",
            toValue: "2026-10-27T00:00");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("yyyy-MM-ddTHH:mm", badRequest.Value?.ToString());
    }
}
