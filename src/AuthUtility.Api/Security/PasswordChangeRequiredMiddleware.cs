using AuthUtility.Infrastructure.Security;
using Microsoft.AspNetCore.Http;

namespace AuthUtility.Api.Security;

public sealed class PasswordChangeRequiredMiddleware(RequestDelegate next)
{
    public static bool IsAllowedPath(PathString path)
    {
        return path.Equals("/api/auth/change-password", StringComparison.OrdinalIgnoreCase)
               || path.Equals("/api/auth/me", StringComparison.OrdinalIgnoreCase);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var mustChange = context.User.FindFirst(AuthClaimNames.MustChangePassword)?.Value;
        var requiresChange = bool.TryParse(mustChange, out var parsed) && parsed;

        if (!requiresChange
            || HttpMethods.IsOptions(context.Request.Method)
            || IsAllowedPath(context.Request.Path))
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            message = "Password wajib diganti sebelum menggunakan aplikasi."
        });
    }
}
