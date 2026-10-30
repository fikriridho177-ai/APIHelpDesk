using AuthUtility.Api.Configuration;

namespace AuthUtility.Tests;

public sealed class FrontendCorsPolicyTests
{
    [Fact]
    public void Build_includes_configured_production_origin_and_normalizes_trailing_slash()
    {
        var origins = FrontendCorsPolicy.Build(
        [
            "http://helpdesk-36-88-241-11.sslip.io/",
            "http://helpdesk-36-88-241-11.sslip.io/"
        ]);

        Assert.Contains(
            "http://helpdesk-36-88-241-11.sslip.io",
            origins);
        Assert.DoesNotContain(
            origins,
            origin => origin.EndsWith("/", StringComparison.Ordinal));
        Assert.Equal(
            origins.Length,
            origins.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
