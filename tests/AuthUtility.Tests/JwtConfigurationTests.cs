using AuthUtility.Infrastructure.Security;

namespace AuthUtility.Tests;

public class JwtConfigurationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short-key")]
    public void IsStrongEnough_RejectsMissingOrShortKeys(string? key)
    {
        Assert.False(JwtConfiguration.IsStrongEnough(key));
    }

    [Fact]
    public void IsStrongEnough_AcceptsAtLeast32Utf8Bytes()
    {
        Assert.True(JwtConfiguration.IsStrongEnough("0123456789abcdef0123456789abcdef"));
    }
}
