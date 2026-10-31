using AuthUtility.Domain.Entities;

namespace AuthUtility.Tests;

public class MenuTypesTests
{
    [Theory]
    [InlineData("widget", "WIDGET")]
    [InlineData(" GROUP ", "GROUP")]
    [InlineData("menu", "MENU")]
    [InlineData("xyz", "MENU")]
    [InlineData(null, "MENU")]
    public void Normalize_MapsToKnownType(string? input, string expected)
    {
        Assert.Equal(expected, MenuTypes.Normalize(input));
    }
}
