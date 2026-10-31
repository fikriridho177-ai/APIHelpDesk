using System.Security.Claims;
using AuthUtility.Application.DTOs.Security;

namespace AuthUtility.Application.Interfaces;

public enum MenuPermission
{
    View,
    Read,
    Create,
    Update,
    Delete,
    Approve,
    Export
}

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(ClaimsPrincipal principal, string menuCode, MenuPermission permission);
    Task<CurrentMenuPermissionDto?> GetPermissionsAsync(ClaimsPrincipal principal, string menuCode);
}
