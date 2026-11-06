using System.Security.Claims;
using AuthUtility.Application.DTOs.Security;
using AuthUtility.Application.Interfaces;
using AuthUtility.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Infrastructure.Security;

public class PermissionService(AppDbContext db) : IPermissionService
{
    public async Task<bool> IsSuperAdminAsync(ClaimsPrincipal principal)
    {
        var rawUserId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(rawUserId, out var userId)) return false;

        return await db.Users
            .AsNoTracking()
            .Where(x => x.Id == userId && x.Active)
            .Select(x => x.Group != null && x.Group.Active && x.Group.IsSuperAdmin)
            .SingleOrDefaultAsync();
    }

    public async Task<CurrentMenuPermissionDto?> GetPermissionsAsync(ClaimsPrincipal principal, string menuCode)
    {
        // var rawUserId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var rawUserId =
    principal.FindFirst(
        ClaimTypes.NameIdentifier
    )?.Value;
        if (!int.TryParse(rawUserId, out var userId)) return null;

        var user = await db.Users
            .AsNoTracking()
            .Include(x => x.Group)
            .FirstOrDefaultAsync(x => x.Id == userId && x.Active);

        if (user?.Group == null || !user.Group.Active) return null;

        var menu = await db.Menus
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Active && x.Code == menuCode);

        if (menu == null) return null;

        if (user.Group.IsSuperAdmin)
        {
            return new CurrentMenuPermissionDto
            {
                MenuCode = menu.Code,
                IsSuperAdmin = true,
                CanView = true,
                CanRead = true,
                CanCreate = true,
                CanUpdate = true,
                CanDelete = true,
                CanApprove = true,
                CanExport = true
            };
        }

        var right = await db.GroupMenus
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.GroupId == user.GroupId && x.MenuId == menu.Id);

        return new CurrentMenuPermissionDto
        {
            MenuCode = menu.Code,
            IsSuperAdmin = false,
            CanView = right?.CanView ?? false,
            CanRead = right?.CanRead ?? false,
            CanCreate = right?.CanCreate ?? false,
            CanUpdate = right?.CanUpdate ?? false,
            CanDelete = right?.CanDelete ?? false,
            CanApprove = right?.CanApprove ?? false,
            CanExport = right?.CanExport ?? false
        };
    }

    public async Task<bool> HasPermissionAsync(ClaimsPrincipal principal, string menuCode, MenuPermission permission)
    {
        var p = await GetPermissionsAsync(principal, menuCode);
        if (p == null) return false;

        return permission switch
        {
            MenuPermission.View => p.CanView,
            MenuPermission.Read => p.CanRead,
            MenuPermission.Create => p.CanCreate,
            MenuPermission.Update => p.CanUpdate,
            MenuPermission.Delete => p.CanDelete,
            MenuPermission.Approve => p.CanApprove,
            MenuPermission.Export => p.CanExport,
            _ => false
        };
    }
}
