namespace AuthUtility.Api.Configuration;

using Microsoft.AspNetCore.Cors.Infrastructure;

public static class FrontendCorsPolicy
{
    private static readonly string[] DefaultOrigins =
    [
        "http://localhost:5173",
        "http://127.0.0.1:5173",
        "http://localhost:5174",
        "http://127.0.0.1:5174",
        "http://192.168.9.23:5173",
        "https://myhelpdesk.wanmixc.dev",
        "http://myhelpdesk.wanmixc.dev"
    ];

    public static string[] Build(IEnumerable<string?>? configuredOrigins)
    {
        return DefaultOrigins
            .Concat(configuredOrigins ?? [])
            .Select(Normalize)
            .Where(origin => origin is not null)
            .Select(origin => origin!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static void Configure(
        CorsPolicyBuilder policy,
        IEnumerable<string?>? configuredOrigins)
    {
        policy.WithOrigins(Build(configuredOrigins))
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    }

    private static string? Normalize(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
            return null;

        var value = origin.Trim().TrimEnd('/');

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            return null;

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(uri.AbsolutePath.Trim('/'))
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
        {
            return null;
        }

        return value;
    }
}
