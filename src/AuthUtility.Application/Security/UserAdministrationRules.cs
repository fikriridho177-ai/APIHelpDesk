namespace AuthUtility.Application.Security;

public static class UserAdministrationRules
{
    public static bool CanChangeGroup(int currentGroupId, int requestedGroupId, bool isSuperAdmin)
    {
        return isSuperAdmin || currentGroupId == requestedGroupId;
    }
}
