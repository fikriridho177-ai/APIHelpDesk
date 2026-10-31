using AuthUtility.Application.DTOs.Groups;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Api.Controllers;

[ApiController]
[Route("api/utility/groups")]
[Authorize]
public class GroupsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var groups = await db.Groups
            .AsNoTracking()
            .OrderBy(x => x.Code)
            .Select(x => new GroupDto(
                x.Id, x.Code, x.Name, x.Description, x.IsSuperAdmin,
                x.AllCompanies, x.AllDepartments, x.AllItemSubCategories,
                x.Active, x.CreatedBy, x.CreatedAt, x.UpdatedBy, x.UpdatedAt
            ))
            .ToListAsync();

        return Ok(groups);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var group = await db.Groups.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return group == null ? NotFound() : Ok(ToDto(group));
    }

    [HttpPost]
    public async Task<IActionResult> Create(SaveGroupRequest r)
    {
        var code = r.Code.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(r.Name))
            return BadRequest(new { message = "Kode dan nama group wajib diisi." });

        if (await db.Groups.AnyAsync(x => x.Code.ToUpper() == code))
            return Conflict(new { message = "Kode group sudah ada" });

        var group = new UtlGroup
        {
            Code = code,
            Name = r.Name.Trim(),
            Description = r.Description?.Trim(),
            IsSuperAdmin = r.IsSuperAdmin,
            Active = r.Active,
            CreatedBy = User.Identity?.Name ?? "system",
            CreatedAt = DbNow()
        };

        db.Groups.Add(group);
        await db.SaveChangesAsync();
        return Ok(ToDto(group));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, SaveGroupRequest r)
    {
        var group = await db.Groups.FindAsync(id);
        if (group == null) return NotFound();

        var code = r.Code.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(r.Name))
            return BadRequest(new { message = "Kode dan nama group wajib diisi." });

        if (await db.Groups.AnyAsync(x => x.Id != id && x.Code.ToUpper() == code))
            return Conflict(new { message = "Kode group sudah digunakan group lain." });

        group.Code = code;
        group.Name = r.Name.Trim();
        group.Description = r.Description?.Trim();
        group.IsSuperAdmin = r.IsSuperAdmin;
        group.Active = r.Active;
        group.UpdatedBy = User.Identity?.Name ?? "system";
        group.UpdatedAt = DbNow();

        await db.SaveChangesAsync();
        return Ok(ToDto(group));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Disable(int id)
    {
        var group = await db.Groups.FindAsync(id);
        if (group == null) return NotFound();

        group.Active = false;
        group.UpdatedBy = User.Identity?.Name ?? "system";
        group.UpdatedAt = DbNow();
        await db.SaveChangesAsync();

        return Ok(new { message = "Hak akses berhasil dinonaktifkan." });
    }

    [HttpGet("{groupId:int}/menus")]
    public async Task<IActionResult> GetMenuAccess(int groupId)
    {
        var groupExists = await db.Groups.AsNoTracking().AnyAsync(x => x.Id == groupId);
        if (!groupExists) return NotFound(new { message = "Group tidak ditemukan." });

        var rights = await db.GroupMenus.AsNoTracking()
            .Where(x => x.GroupId == groupId)
            .ToDictionaryAsync(x => x.MenuId);

        var menus = await db.Menus.AsNoTracking()
            .Where(x => x.Active)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();

        var group = await db.Groups.AsNoTracking().FirstAsync(x => x.Id == groupId);
        var result = menus.Select(m =>
        {
            rights.TryGetValue(m.Id, out var r);
            var full = group.IsSuperAdmin;
            return new GroupMenuAccessDto
            {
                MenuId = m.Id,
                ParentId = m.ParentId,
                Code = m.Code,
                Name = m.Name,
                MenuType = m.MenuType,
                SortOrder = m.SortOrder,
                CanView = full || (r?.CanView ?? false),
                CanRead = full || (r?.CanRead ?? false),
                CanCreate = full || (r?.CanCreate ?? false),
                CanUpdate = full || (r?.CanUpdate ?? false),
                CanDelete = full || (r?.CanDelete ?? false),
                CanApprove = full || (r?.CanApprove ?? false),
                CanExport = full || (r?.CanExport ?? false)
            };
        });

        return Ok(result);
    }

    [HttpPut("{groupId:int}/menus")]
    public async Task<IActionResult> SaveMenuAccess(int groupId, SaveGroupMenuAccessRequest request)
    {
        var group = await db.Groups.FindAsync(groupId);
        if (group == null) return NotFound(new { message = "Group tidak ditemukan." });
        if (group.IsSuperAdmin) return Ok(new { message = "Super Administrator selalu memiliki seluruh akses menu." });

        var menus = await db.Menus.Where(x => x.Active).ToListAsync();
        var menuById = menus.ToDictionary(x => x.Id);
        var requested = request.Permissions
            .Where(x => menuById.ContainsKey(x.MenuId))
            .GroupBy(x => x.MenuId)
            .Select(x => x.Last())
            .ToDictionary(x => x.MenuId);

        // Permission aksi otomatis membutuhkan View agar halaman dapat dibuka.
        foreach (var item in requested.Values)
        {
            if (item.CanRead || item.CanCreate || item.CanUpdate || item.CanDelete || item.CanApprove || item.CanExport)
                item.CanView = true;
        }

        // Child yang terlihat membutuhkan seluruh ancestor agar navigation tetap utuh.
        var visibleIds = requested.Values.Where(x => x.CanView).Select(x => x.MenuId).ToHashSet();
        var currentIds = visibleIds.ToList();
        foreach (var id in currentIds)
        {
            var cursor = menuById[id];
            while (cursor.ParentId.HasValue && menuById.TryGetValue(cursor.ParentId.Value, out var parent))
            {
                visibleIds.Add(parent.Id);
                cursor = parent;
            }
        }

        foreach (var id in visibleIds)
        {
            if (requested.TryGetValue(id, out var existing)) existing.CanView = true;
            else requested[id] = new SaveGroupMenuPermissionRequest { MenuId = id, CanView = true };
        }

        await using var tx = await db.Database.BeginTransactionAsync();
        var old = await db.GroupMenus.Where(x => x.GroupId == groupId).ToListAsync();
        db.GroupMenus.RemoveRange(old);

        var rows = requested.Values
            .Where(x => x.CanView || x.CanRead || x.CanCreate || x.CanUpdate || x.CanDelete || x.CanApprove || x.CanExport)
            .Select(x => new UtlGroupMenu
            {
                GroupId = groupId,
                MenuId = x.MenuId,
                CanView = x.CanView,
                CanRead = x.CanRead,
                CanCreate = x.CanCreate,
                CanUpdate = x.CanUpdate,
                CanDelete = x.CanDelete,
                CanApprove = x.CanApprove,
                CanExport = x.CanExport
            });

        db.GroupMenus.AddRange(rows);
        group.UpdatedBy = User.Identity?.Name ?? "system";
        group.UpdatedAt = DbNow();
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new { message = "Akses menu berhasil disimpan." });
    }

    [HttpGet("{groupId:int}/companies")]
    public async Task<IActionResult> GetCompanies(int groupId)
    {
        var group = await db.Groups
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == groupId);

        if (group == null) return NotFound();

        var selected = await db.GroupCompanies
            .AsNoTracking()
            .Where(x => x.GroupId == groupId)
            .ToListAsync();

        var selectedCodes = selected.Select(x => x.CompanyId.ToString()).ToList();

        return Ok(new ExternalGroupScopeDto
        {
            All = group.IsSuperAdmin || group.AllCompanies,
            SelectedIds = group.IsSuperAdmin ? new List<string>() : selectedCodes
        });
    }

    [HttpPut("{groupId:int}/companies")]
    public async Task<IActionResult> SaveCompanies(
        int groupId,
        SaveExternalGroupScopeRequest request
    )
    {
        var group = await db.Groups.FindAsync(groupId);

        if (group == null)
            return NotFound(new { message = "Group tidak ditemukan." });

        if (group.IsSuperAdmin)
            return Ok(new
            {
                message = "Super Administrator selalu memiliki seluruh akses perusahaan anak."
            });

        request.SelectedIds ??= new List<string>();

        var selectedIds = request.All
            ? new List<string>()
            : request.SelectedIds
                .Select(x => x?.Trim() ?? string.Empty)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        // Kolom UtlGroupCompany.CompanyId di database bertipe integer.
        if (!TryParseIds(selectedIds, out var companyIds, out var invalidCompany))
            return BadRequest(new { message = $"Kode perusahaan '{invalidCompany}' tidak valid (harus angka)." });

        await using var tx = await db.Database.BeginTransactionAsync();

        await db.GroupCompanies
            .Where(x => x.GroupId == groupId)
            .ExecuteDeleteAsync();

        if (!request.All)
        {
            db.GroupCompanies.AddRange(
                companyIds.Select(id => new UtlGroupCompany
                {
                    GroupId = groupId,
                    CompanyId = id
                })
            );
        }

        group.AllCompanies = request.All;
        group.UpdatedBy = User.Identity?.Name ?? "system";
        group.UpdatedAt = DbNow();

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new { message = "Akses perusahaan anak berhasil disimpan." });
    }

    [HttpGet("{groupId:int}/departments")]
    public async Task<IActionResult> GetDepartments(int groupId)
    {
        var group = await db.Groups
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == groupId);

        if (group == null) return NotFound();

        var selected = await db.GroupDepartments
            .AsNoTracking()
            .Where(x => x.GroupId == groupId)
            .ToListAsync();

        var selectedCodes = selected.Select(x => x.DepartmentId.ToString()).ToList();

        return Ok(new ExternalGroupScopeDto
        {
            All = group.IsSuperAdmin || group.AllDepartments,
            SelectedIds = group.IsSuperAdmin ? new List<string>() : selectedCodes
        });
    }

    [HttpPut("{groupId:int}/departments")]
    public async Task<IActionResult> SaveDepartments(
        int groupId,
        SaveExternalGroupScopeRequest request
    )
    {
        var group = await db.Groups.FindAsync(groupId);

        if (group == null)
            return NotFound(new { message = "Group tidak ditemukan." });

        if (group.IsSuperAdmin)
            return Ok(new
            {
                message = "Super Administrator selalu memiliki seluruh akses departemen."
            });

        request.SelectedIds ??= new List<string>();

        var selectedIds = request.All
            ? new List<string>()
            : request.SelectedIds
                .Select(x => x?.Trim() ?? string.Empty)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        // Kolom UtlGroupDepartment.DepartmentId di database bertipe integer.
        if (!TryParseIds(selectedIds, out var departmentIds, out var invalidDepartment))
            return BadRequest(new { message = $"Kode departemen '{invalidDepartment}' tidak valid (harus angka)." });

        await using var tx = await db.Database.BeginTransactionAsync();

        await db.GroupDepartments
            .Where(x => x.GroupId == groupId)
            .ExecuteDeleteAsync();

        if (!request.All)
        {
            db.GroupDepartments.AddRange(
                departmentIds.Select(id => new UtlGroupDepartment
                {
                    GroupId = groupId,
                    DepartmentId = id
                })
            );
        }

        group.AllDepartments = request.All;
        group.UpdatedBy = User.Identity?.Name ?? "system";
        group.UpdatedAt = DbNow();

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new { message = "Akses departemen berhasil disimpan." });
    }

    [HttpGet("{groupId:int}/item-sub-categories")]
    public async Task<IActionResult> GetItemSubCategories(int groupId)
    {
        var group = await db.Groups.AsNoTracking().FirstOrDefaultAsync(x => x.Id == groupId);
        if (group == null) return NotFound();
        var selected = await db.GroupItemSubCategories.AsNoTracking().Where(x => x.GroupId == groupId).Select(x => x.ItemSubCategoryId).ToListAsync();
        var options = await db.ItemSubCategories.AsNoTracking().Where(x => x.Active).OrderBy(x => x.Code).Select(x => new ScopeOptionDto(x.Id, x.Code, x.Name)).ToListAsync();
        return Ok(new GroupScopeDto { All = group.IsSuperAdmin || group.AllItemSubCategories, SelectedIds = group.IsSuperAdmin ? options.Select(x => x.Id).ToList() : selected, Options = options });
    }

    [HttpPut("{groupId:int}/item-sub-categories")]
    public async Task<IActionResult> SaveItemSubCategories(int groupId, SaveGroupScopeRequest request)
    {
        var group = await db.Groups.FindAsync(groupId);
        if (group == null) return NotFound(new { message = "Group tidak ditemukan." });
        if (group.IsSuperAdmin) return Ok(new { message = "Super Administrator selalu memiliki seluruh akses item sub kategori." });

        request.SelectedIds ??= new List<int>();
        var validIds = request.All
            ? new List<int>()
            : await db.ItemSubCategories
                .Where(x => x.Active && request.SelectedIds.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync();

        await using var tx = await db.Database.BeginTransactionAsync();
        await db.GroupItemSubCategories
            .Where(x => x.GroupId == groupId)
            .ExecuteDeleteAsync();

        if (!request.All)
        {
            db.GroupItemSubCategories.AddRange(validIds.Distinct().Select(id => new UtlGroupItemSubCategory
            {
                GroupId = groupId,
                ItemSubCategoryId = id
            }));
        }

        group.AllItemSubCategories = request.All;
        group.UpdatedBy = User.Identity?.Name ?? "system";
        group.UpdatedAt = DbNow();

        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Ok(new { message = "Akses item sub kategori berhasil disimpan." });
    }

    private static DateTime DbNow() => DateTime.UtcNow;

    private static bool TryParseIds(IEnumerable<string> values, out List<int> ids, out string? invalid)
    {
        ids = new List<int>();
        invalid = null;

        foreach (var value in values)
        {
            if (!int.TryParse(value, out var id))
            {
                invalid = value;
                return false;
            }

            ids.Add(id);
        }

        // "01" dan "1" menjadi nilai yang sama setelah dikonversi.
        ids = ids.Distinct().ToList();
        return true;
    }

    private static GroupDto ToDto(UtlGroup x) => new(
        x.Id, x.Code, x.Name, x.Description, x.IsSuperAdmin,
        x.AllCompanies, x.AllDepartments, x.AllItemSubCategories,
        x.Active, x.CreatedBy, x.CreatedAt, x.UpdatedBy, x.UpdatedAt
    );
}
