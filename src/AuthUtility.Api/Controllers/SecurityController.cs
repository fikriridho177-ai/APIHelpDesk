using AuthUtility.Application.DTOs.Security;
using AuthUtility.Application.Interfaces;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Api.Controllers;

[ApiController]
[Route("api/utility/security")]
[Authorize]
public class SecurityController(
    AppDbContext db,
    IPermissionService permissionService
) : ControllerBase
{

    [HttpGet("me/{menuCode}")]
    public async Task<IActionResult> MyPermission(string menuCode)
    {
        var result = await permissionService.GetPermissionsAsync(User, menuCode);
        return result == null ? Forbid() : Ok(result);
    }

    [HttpGet("menus")]
    public async Task<IActionResult> Menus()
    {
        if (!await permissionService.IsSuperAdminAsync(User)) return Forbid();

        var menus = await db.Menus
            .OrderBy(x => x.SortOrder)
            .Select(
                x => new MenuDto(
                    x.Id,
                    x.Code,
                    x.Name,
                    x.Route,
                    x.SortOrder,
                    x.Active
                )
            )
            .ToListAsync();

        return Ok(menus);
    }


    [HttpGet("groups/{groupId:int}")]
    public async Task<IActionResult> GroupSecurity(
        int groupId
    )
    {
        if (!await permissionService.IsSuperAdminAsync(User)) return Forbid();

        var menus = await db.Menus
            .OrderBy(x => x.SortOrder)
            .ToListAsync();

        var rights = await db.GroupMenus
            .Where(
                x => x.GroupId == groupId
            )
            .ToDictionaryAsync(
                x => x.MenuId
            );

        var result = menus.Select(m =>
        {
            rights.TryGetValue(
                m.Id,
                out var r
            );

            return new GroupMenuDto(
                m.Id,
                m.Code,
                m.Name,

                r?.CanView ?? false,
                r?.CanCreate ?? false,
                r?.CanUpdate ?? false,
                r?.CanDelete ?? false,
                r?.CanApprove ?? false
            );
        });

        return Ok(result);
    }


    [HttpPut("groups/{groupId:int}")]
    public async Task<IActionResult> Save(
        int groupId,
        List<SaveGroupMenuRequest> items
    )
    {
        if (!await permissionService.IsSuperAdminAsync(User)) return Forbid();

        var old = await db.GroupMenus
            .Where(
                x => x.GroupId == groupId
            )
            .ToListAsync();

        db.GroupMenus.RemoveRange(old);

        var newItems = items.Select(
            x => new UtlGroupMenu
            {
                GroupId = groupId,
                MenuId = x.MenuId,

                CanView = x.CanView,
                CanCreate = x.CanCreate,
                CanUpdate = x.CanUpdate,
                CanDelete = x.CanDelete,
                CanApprove = x.CanApprove
            }
        );

        db.GroupMenus.AddRange(newItems);

        await db.SaveChangesAsync();

        return Ok(new
        {
            message = "Security group berhasil disimpan"
        });
    }
}
