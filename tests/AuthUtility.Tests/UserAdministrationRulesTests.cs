using AuthUtility.Application.Security;

namespace AuthUtility.Tests;

public class UserAdministrationRulesTests
{
    [Fact]
    public void CanChangeGroup_AllowsSuperAdmin()
    {
        Assert.True(UserAdministrationRules.CanChangeGroup(1, 2, isSuperAdmin: true));
    }

    [Fact]
    public void CanChangeGroup_AllowsNonSuperAdminToKeepExistingGroup()
    {
        Assert.True(UserAdministrationRules.CanChangeGroup(1, 1, isSuperAdmin: false));
    }

    [Fact]
    public void CanChangeGroup_RejectsNonSuperAdminGroupChange()
    {
        Assert.False(UserAdministrationRules.CanChangeGroup(1, 2, isSuperAdmin: false));
    }
}
