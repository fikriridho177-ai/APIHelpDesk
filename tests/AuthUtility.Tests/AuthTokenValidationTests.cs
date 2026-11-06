using AuthUtility.Infrastructure.Security;

namespace AuthUtility.Tests;

public class AuthTokenValidationTests
{
    [Fact]
    public void PasswordStamp_IsCurrentWhenTokenMatchesDatabase()
    {
        var changedAt = new DateTime(2026, 11, 5, 10, 30, 0, DateTimeKind.Utc);

        Assert.True(AuthTokenValidation.IsPasswordStampCurrent(
            changedAt,
            AuthTokenValidation.ToPasswordStamp(changedAt)));
    }

    [Fact]
    public void PasswordStamp_RejectsTokenIssuedBeforePasswordChange()
    {
        var changedAt = new DateTime(2026, 11, 5, 10, 30, 0, DateTimeKind.Utc);

        Assert.False(AuthTokenValidation.IsPasswordStampCurrent(changedAt, "0"));
        Assert.False(AuthTokenValidation.IsPasswordStampCurrent(changedAt, "not-a-timestamp"));
    }

    [Fact]
    public void PasswordStamp_AllowsLegacyUserWithoutPasswordChangeTimestamp()
    {
        Assert.True(AuthTokenValidation.IsPasswordStampCurrent(null, null));
        Assert.True(AuthTokenValidation.IsPasswordStampCurrent(null, "0"));
    }

    [Fact]
    public void PasswordStamp_NormalizesSubMicrosecondPrecision()
    {
        var changedAt = new DateTime(2026, 11, 5, 10, 30, 0, 123, DateTimeKind.Utc)
            .AddTicks(7);
        var normalizedTicks = changedAt.Ticks - (changedAt.Ticks % 10);

        Assert.Equal(
            normalizedTicks.ToString(),
            AuthTokenValidation.ToPasswordStamp(changedAt));
    }
}
