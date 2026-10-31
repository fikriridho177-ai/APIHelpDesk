using AuthUtility.Application.DTOs.Menus;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AuthUtility.Api.Controllers;


[ApiController]
[Authorize]
[Route("api/utility/menus")]
public class MenusController : ControllerBase
{
    private readonly AppDbContext _db;


    public MenusController(
        AppDbContext db
    )
    {
        _db = db;
    }


    /* =========================================================
       GET ALL
       GET /api/utility/menus
    ========================================================= */

    [HttpGet]
    public async Task<ActionResult<List<MenuDto>>> GetAll()
    {
        var data =
            await _db.Menus

                .AsNoTracking()

                .Include(x => x.GroupMenus)
                    .ThenInclude(x => x.Group)

                .OrderBy(x => x.SortOrder)

                .ThenBy(x => x.Name)

                .ToListAsync();


        var result =
            data.Select(MapMenu)
                .ToList();


        return Ok(result);
    }


    /* =========================================================
       GET BY ID
       GET /api/utility/menus/1
    ========================================================= */

    [HttpGet("{id:int}")]
    public async Task<ActionResult<MenuDto>> GetById(
        int id
    )
    {
        var menu =
            await _db.Menus

                .AsNoTracking()

                .Include(x => x.GroupMenus)
                    .ThenInclude(x => x.Group)

                .FirstOrDefaultAsync(
                    x => x.Id == id
                );


        if (menu == null)
        {
            return NotFound(
                new
                {
                    message =
                        "Menu tidak ditemukan."
                }
            );
        }


        return Ok(
            MapMenu(menu)
        );
    }


    /* =========================================================
       CREATE
       POST /api/utility/menus
    ========================================================= */

    [HttpPost]
    public async Task<ActionResult<MenuDto>> Create(
        SaveMenuRequest request
    )
    {
        if (
            string.IsNullOrWhiteSpace(
                request.Code
            )
        )
        {
            return BadRequest(
                new
                {
                    message =
                        "Kode menu wajib diisi."
                }
            );
        }


        if (
            string.IsNullOrWhiteSpace(
                request.Name
            )
        )
        {
            return BadRequest(
                new
                {
                    message =
                        "Nama menu wajib diisi."
                }
            );
        }


        var code =
            request.Code
                .Trim()
                .ToUpper();


        var exists =
            await _db.Menus.AnyAsync(
                x =>
                    x.Code.ToUpper()
                        == code
            );


        if (exists)
        {
            return BadRequest(
                new
                {
                    message =
                        "Kode menu sudah digunakan."
                }
            );
        }


        if (
            request.ParentId.HasValue
        )
        {
            var parentExists =
                await _db.Menus.AnyAsync(
                    x =>
                        x.Id
                            == request.ParentId.Value
                        &&
                        x.Active
                );


            if (!parentExists)
            {
                return BadRequest(
                    new
                    {
                        message =
                            "Parent menu tidak ditemukan."
                    }
                );
            }
        }


        var menuType =
            NormalizeMenuType(
                request.MenuType
            );


        var userId =
            User.Identity?.Name
            ?? "system";


        var menu =
            new UtlMenu
            {
                ParentId =
                    request.ParentId,

                Code =
                    code,

                Name =
                    request.Name.Trim(),

                Route =
                    menuType == MenuTypes.Widget ? null : NormalizeRoute(request.Route),

                Icon =
                    request.Icon?.Trim(),

                FullName =
                    EmptyToNull(request.FullName),

                Description =
                    EmptyToNull(request.Description),

                Color =
                    EmptyToNull(request.Color)?.ToLower(),

                MenuType =
                    menuType,

                SortOrder =
                    request.SortOrder,

                Active =
                    request.Active,

                CreatedBy =
                    userId,

                CreatedAt =
                    DateTime.UtcNow
            };


        _db.Menus.Add(menu);

        await _db.SaveChangesAsync();



        var result =
            await GetMenuDto(
                menu.Id
            );


        return CreatedAtAction(
            nameof(GetById),

            new
            {
                id = menu.Id
            },

            result
        );
    }


    /* =========================================================
       UPDATE
       PUT /api/utility/menus/1
    ========================================================= */

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        SaveMenuRequest request
    )
    {
        var menu =
            await _db.Menus
                .FirstOrDefaultAsync(
                    x => x.Id == id
                );


        if (menu == null)
        {
            return NotFound(
                new
                {
                    message =
                        "Menu tidak ditemukan."
                }
            );
        }


        if (
            request.ParentId == id
        )
        {
            return BadRequest(
                new
                {
                    message =
                        "Menu tidak boleh menjadi parent untuk dirinya sendiri."
                }
            );
        }


        var code =
            request.Code
                .Trim()
                .ToUpper();


        var duplicate =
            await _db.Menus.AnyAsync(
                x =>
                    x.Id != id
                    &&
                    x.Code.ToUpper()
                        == code
            );


        if (duplicate)
        {
            return BadRequest(
                new
                {
                    message =
                        "Kode menu sudah digunakan."
                }
            );
        }


        if (
            request.ParentId.HasValue
        )
        {
            var parentExists =
                await _db.Menus.AnyAsync(
                    x =>
                        x.Id
                            == request.ParentId.Value
                        &&
                        x.Active
                );


            if (!parentExists)
            {
                return BadRequest(
                    new
                    {
                        message =
                            "Parent menu tidak ditemukan."
                    }
                );
            }
        }


        var menuType =
            NormalizeMenuType(
                request.MenuType
            );


        menu.ParentId =
            request.ParentId;

        menu.Code =
            code;

        menu.Name =
            request.Name.Trim();

        menu.Route =
            menuType == MenuTypes.Widget ? null : NormalizeRoute(request.Route);

        menu.Icon =
            request.Icon?.Trim();

        menu.FullName =
            EmptyToNull(request.FullName);

        menu.Description =
            EmptyToNull(request.Description);

        menu.Color =
            EmptyToNull(request.Color)?.ToLower();

        menu.MenuType =
            menuType;

        menu.SortOrder =
            request.SortOrder;

        menu.Active =
            request.Active;

        menu.UpdatedBy =
            User.Identity?.Name
            ?? "system";

        menu.UpdatedAt =
            DateTime.UtcNow;


        await _db.SaveChangesAsync();



        return NoContent();
    }


    /* =========================================================
       NONACTIVE
       DELETE /api/utility/menus/1
    ========================================================= */

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Disable(
        int id
    )
    {
        var menu =
            await _db.Menus
                .FirstOrDefaultAsync(
                    x => x.Id == id
                );


        if (menu == null)
        {
            return NotFound(
                new
                {
                    message =
                        "Menu tidak ditemukan."
                }
            );
        }


        menu.Active = false;

        menu.UpdatedBy =
            User.Identity?.Name
            ?? "system";

        menu.UpdatedAt =
            DateTime.UtcNow;


        await _db.SaveChangesAsync();


        return NoContent();
    }


    /* =========================================================
       GET MENU DTO
    ========================================================= */

    private async Task<MenuDto?> GetMenuDto(
        int id
    )
    {
        var menu =
            await _db.Menus

                .AsNoTracking()

                .Include(x => x.GroupMenus)
                    .ThenInclude(x => x.Group)

                .FirstOrDefaultAsync(
                    x => x.Id == id
                );


        if (menu == null)
        {
            return null;
        }


        return MapMenu(menu);
    }


    /* =========================================================
       MAPPING
    ========================================================= */

    private static MenuDto MapMenu(
        UtlMenu menu
    )
    {
        return new MenuDto
        {
            Id =
                menu.Id,

            ParentId =
                menu.ParentId,

            Code =
                menu.Code,

            Name =
                menu.Name,

            Route =
                menu.Route,

            Icon =
                menu.Icon,

            FullName =
                menu.FullName,

            Description =
                menu.Description,

            Color =
                menu.Color,

            MenuType =
                menu.MenuType,

            SortOrder =
                menu.SortOrder,

            Active =
                menu.Active,

            CreatedBy =
                menu.CreatedBy,

            CreatedAt =
                menu.CreatedAt,

            UpdatedBy =
                menu.UpdatedBy,

            UpdatedAt =
                menu.UpdatedAt,

            Permissions =
                menu.GroupMenus

                    .Where(
                        x =>
                            x.Group != null
                    )

                    .Select(
                        x =>
                            new MenuPermissionDto
                            {
                                GroupId =
                                    x.GroupId,

                                GroupCode =
                                    x.Group!.Code,

                                GroupName =
                                    x.Group.Name,

                                CanView =
                                    x.CanView,

                                CanRead =
                                    x.CanRead,

                                CanCreate =
                                    x.CanCreate,

                                CanUpdate =
                                    x.CanUpdate,

                                CanDelete =
                                    x.CanDelete,

                                CanApprove =
                                    x.CanApprove,

                                CanExport =
                                    x.CanExport
                            }
                    )

                    .OrderBy(
                        x => x.GroupName
                    )

                    .ToList()
        };
    }


    /* =========================================================
       HELPERS
    ========================================================= */

    private static string NormalizeMenuType(
        string? value
    )
    {
        // GROUP, MENU atau WIDGET (widget portal).
        return MenuTypes.Normalize(value);
    }


    private static string? NormalizeRoute(
        string? value
    )
    {
        if (
            string.IsNullOrWhiteSpace(
                value
            )
        )
        {
            return null;
        }


        var route =
            value.Trim();


        if (
            !route.StartsWith("/")
        )
        {
            route =
                "/" + route;
        }


        return route;
    }


    private static string? EmptyToNull(
        string? value
    )
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    /* =========================================================
   NAVIGATION MENU USER LOGIN

   GET /api/utility/menus/navigation
========================================================= */

[HttpGet("navigation")]
public async Task<IActionResult> GetNavigation()
{
    var userIdClaim =
        User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );

    if (
        string.IsNullOrWhiteSpace(userIdClaim)
        ||
        !int.TryParse(
            userIdClaim,
            out var userId
        )
    )
    {
        return Unauthorized();
    }


    var user = await _db.Users
        .AsNoTracking()
        .Include(x => x.Group)
        .FirstOrDefaultAsync(
            x =>
                x.Id == userId
                &&
                x.Active
        );


    if (user == null)
    {
        return Unauthorized();
    }


    /* =====================================================
       AMBIL SEMUA MENU AKTIF
    ===================================================== */

    var allMenus = await _db.Menus
        .AsNoTracking()
        .Where(x => x.Active)
        .OrderBy(x => x.SortOrder)
        .ThenBy(x => x.Name)
        .Select(x =>
            new NavigationMenuDto
            {
                Id = x.Id,

                ParentId = x.ParentId,

                Code = x.Code,

                Name = x.Name,

                Route = x.Route,

                Icon = x.Icon,

                FullName = x.FullName,

                Description = x.Description,

                Color = x.Color,

                MenuType = x.MenuType,

                SortOrder = x.SortOrder
            }
        )
        .ToListAsync();


    /* =====================================================
       SUPER ADMIN

       Mendapat seluruh menu aktif
    ===================================================== */

    if (
        user.Group != null
        &&
        user.Group.IsSuperAdmin
    )
    {
        return Ok(allMenus);
    }


    /* =====================================================
       MENU BERDASARKAN GROUP
    ===================================================== */

    var allowedMenuIds =
        await _db.GroupMenus

            .AsNoTracking()

            .Where(x =>
                x.GroupId == user.GroupId
                &&
                x.CanView
            )

            .Select(x => x.MenuId)

            .ToListAsync();


    var allowedIds =
        allowedMenuIds.ToHashSet();


    /* =====================================================
       PARENT OTOMATIS IKUT DITAMPILKAN

       Contoh:

       Utility tidak diberi akses langsung,
       tetapi Pengguna diberi CanView.

       Utility tetap harus muncul supaya
       Pengguna bisa ditampilkan sebagai child.
    ===================================================== */

    var changed = true;

    while (changed)
    {
        changed = false;

        var parentIds =
            allMenus

                .Where(x =>
                    allowedIds.Contains(x.Id)
                    &&
                    x.ParentId.HasValue
                )

                .Select(x =>
                    x.ParentId!.Value
                )

                .Distinct()

                .ToList();


        foreach (var parentId in parentIds)
        {
            if (allowedIds.Add(parentId))
            {
                changed = true;
            }
        }
    }


    var result =
        allMenus

            .Where(x =>
                allowedIds.Contains(x.Id)
            )

            .OrderBy(x => x.SortOrder)

            .ThenBy(x => x.Name)

            .ToList();


    return Ok(result);
    }

}