using System.Text;

namespace AuthUtility.Infrastructure.Security;

public static class JwtConfiguration
{
    public const int MinimumKeyBytes = 32;

    public static bool IsStrongEnough(string? key)
    {
        return !string.IsNullOrWhiteSpace(key)
               && Encoding.UTF8.GetByteCount(key) >= MinimumKeyBytes;
    }
}
