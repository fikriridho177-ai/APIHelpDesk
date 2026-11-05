using AuthUtility.Api.Configuration;
using Microsoft.AspNetCore.Cors.Infrastructure;

namespace AuthUtility.Tests;

public sealed class FrontendCorsPolicyTests
{
    [Fact]
    public void Configure_allows_credentials_for_configured_frontend_origin()
    {
        var policyBuilder = new CorsPolicyBuilder();

        FrontendCorsPolicy.Configure(
            policyBuilder,
            ["http://myhelpdesk-36-88-241-11.sslip.io"]);

        var policy = policyBuilder.Build();

        Assert.True(policy.SupportsCredentials);
        Assert.Contains(
            "http://myhelpdesk-36-88-241-11.sslip.io",
            policy.Origins);
    }

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
