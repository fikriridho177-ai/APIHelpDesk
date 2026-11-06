using AuthUtility.Api.Security;
using Microsoft.AspNetCore.Http;

namespace AuthUtility.Tests;

public class PasswordChangeRequiredMiddlewareTests
{
    [Theory]
    [InlineData("/api/auth/change-password")]
    [InlineData("/api/auth/me")]
    public void IsAllowedPath_AllowsPasswordRecoveryEndpoints(string path)
    {
        Assert.True(PasswordChangeRequiredMiddleware.IsAllowedPath(new PathString(path)));
    }

    [Fact]
    public void IsAllowedPath_BlocksNormalApiRoutes()
    {
        Assert.False(PasswordChangeRequiredMiddleware.IsAllowedPath(new PathString("/api/utility/users")));
    }
}
