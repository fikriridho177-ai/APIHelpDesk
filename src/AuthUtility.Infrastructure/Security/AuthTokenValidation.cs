using System.Globalization;

namespace AuthUtility.Infrastructure.Security;

public static class AuthTokenValidation
{
    private const long TicksPerMicrosecond = 10;

    public static string ToPasswordStamp(DateTime? changedAt)
    {
        return changedAt == null
            ? "0"
            : NormalizeTicks(changedAt.Value.Ticks).ToString(CultureInfo.InvariantCulture);
    }

    public static bool IsPasswordStampCurrent(DateTime? changedAt, string? tokenStamp)
    {
        if (!changedAt.HasValue)
            return string.IsNullOrWhiteSpace(tokenStamp) || tokenStamp == "0";

        return long.TryParse(
                   tokenStamp,
                   NumberStyles.Integer,
                   CultureInfo.InvariantCulture,
                   out var ticks)
               && ticks == NormalizeTicks(changedAt.Value.Ticks);
    }

    private static long NormalizeTicks(long ticks)
    {
        return ticks - ticks % TicksPerMicrosecond;
    }
}
