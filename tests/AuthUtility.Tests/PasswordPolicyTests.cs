using AuthUtility.Application.Security;

namespace AuthUtility.Tests;

public class PasswordPolicyTests
{
    [Fact]
    public void Validate_RejectsMissingPassword()
    {
        Assert.NotNull(PasswordPolicy.Validate(null));
        Assert.NotNull(PasswordPolicy.Validate("   "));
    }

    [Theory]
    [InlineData("Short1!")]
    [InlineData("alllowercase123!")]
    [InlineData("ALLUPPERCASE123!")]
    [InlineData("NoNumberPassword!")]
    [InlineData("NoSymbolPassword123")]
    public void Validate_RejectsWeakPassword(string password)
    {
        Assert.NotNull(PasswordPolicy.Validate(password));
    }

    [Fact]
    public void Validate_AcceptsPasswordMeetingMinimumPolicy()
    {
        Assert.Null(PasswordPolicy.Validate("Strong-Password-12!"));
    }

    [Fact]
    public void Validate_RejectsPasswordLongerThanMaximum()
    {
        Assert.NotNull(PasswordPolicy.Validate("A" + new string('b', 126) + "1!"));
    }
}
