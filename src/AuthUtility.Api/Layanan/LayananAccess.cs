using AuthUtility.Application.Interfaces;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Api.Layanan;

// Hak akses user saat ini untuk modul Layanan, dimuat sekali per request.
// Aturannya sama dengan PermissionService yang sudah ada:
//   - Super Admin  -> semua permission & semua departemen
//   - Selain itu   -> UtlGroupMenu (Hak Akses > Akses Menu)
//                     + UtlGroupDepartment / AllDepartments (Hak Akses > Akses Departemen)
public class LayananAccess
{
    public const string ServiceRoutePrefix = "/layanan/";

    // Kode menu layanan (divisi & sub-layanan) selalu diawali LYN_. Dipakai agar menu yang dipindah ke parent lain
    // di Manajemen Menu (route-nya ikut berubah) tetap dikenali sebagai layanan.
    public const string ServiceCodePrefix = "LYN_";

    public int UserId { get; private init; }
    public UtlUser User { get; private init; } = null!;
    public bool IsSuperAdmin { get; private init; }

    // Semua menu layanan yang aktif (root LAYANAN, divisi, sub-layanan, daftar pengajuan).
    public Dictionary<int, UtlMenu> Menus { get; private init; } = new();

    private bool _allDepartments;
    private HashSet<int> _departmentIds = new();
    private Dictionary<int, UtlGroupMenu> _rights = new();

    public static async Task<LayananAccess?> LoadAsync(AppDbContext db, int userId)
    {
        var user = await db.Users
            .AsNoTracking()
            .Include(x => x.Group)
            .FirstOrDefaultAsync(x => x.Id == userId && x.Active);

        if (user?.Group == null || !user.Group.Active) return null;

        var menus = await db.Menus
            .AsNoTracking()
            .Where(x => x.Active && (x.Code == "LAYANAN" || x.Code == "LAYANAN_PENGAJUAN" ||
                                     (x.Route != null && x.Route.StartsWith(ServiceRoutePrefix)) ||
                                     x.Code.StartsWith(ServiceCodePrefix)))
            .ToDictionaryAsync(x => x.Id);

        var rights = await db.GroupMenus
            .AsNoTracking()
            .Where(x => x.GroupId == user.GroupId)
            .ToDictionaryAsync(x => x.MenuId);

        var departmentIds = await db.GroupDepartments
            .AsNoTracking()
            .Where(x => x.GroupId == user.GroupId)
            .Select(x => x.DepartmentId)
            .ToListAsync();

        return new LayananAccess
        {
            UserId = userId,
            User = user,
            IsSuperAdmin = user.Group.IsSuperAdmin,
            Menus = menus,
            _allDepartments = user.Group.AllDepartments,
            _departmentIds = departmentIds.ToHashSet(),
            _rights = rights
        };
    }

    // Sub-layanan = menu tipe MENU dengan route /layanan/... atau kode LYN_...
    public bool IsService(UtlMenu menu) =>
        menu.MenuType == "MENU" &&
        ((menu.Route != null && menu.Route.StartsWith(ServiceRoutePrefix)) || menu.Code.StartsWith(ServiceCodePrefix));

    public bool Has(int menuId, MenuPermission permission)
    {
        if (!Menus.ContainsKey(menuId)) return false;
        if (IsSuperAdmin) return true;
        if (!_rights.TryGetValue(menuId, out var right)) return false;

        return permission switch
        {
            MenuPermission.View => right.CanView,
            MenuPermission.Read => right.CanRead,
            MenuPermission.Create => right.CanCreate,
            MenuPermission.Update => right.CanUpdate,
            MenuPermission.Delete => right.CanDelete,
            MenuPermission.Approve => right.CanApprove,
            MenuPermission.Export => right.CanExport,
            _ => false
        };
    }

    // Petugas: boleh melihat pengajuan orang lain di sub-layanan ini.
    public bool IsHandler(int menuId) =>
        Has(menuId, MenuPermission.Read) || Has(menuId, MenuPermission.Approve) || Has(menuId, MenuPermission.Update);

    // Akses Departemen: departemen pemohon termasuk departemen yang diizinkan untuk grup user.
    public bool InDepartmentScope(string? requesterDeptId)
    {
        if (IsSuperAdmin || _allDepartments) return true;
        return int.TryParse(requesterDeptId?.Trim(), out var deptId) && _departmentIds.Contains(deptId);
    }

    public bool CanSee(LynRequest request) =>
        request.RequesterUserId == UserId ||
        (IsHandler(request.MenuId) && InDepartmentScope(request.RequesterDeptId));

    // Nama divisi = menu tepat di bawah root LAYANAN (RNA, GIS, WAD, QC, NAT).
    public string? GetDivisionName(int menuId)
    {
        var visited = new HashSet<int>();
        Menus.TryGetValue(menuId, out var current);

        while (current?.ParentId != null && visited.Add(current.Id))
        {
            if (!Menus.TryGetValue(current.ParentId.Value, out var parent)) break;
            if (parent.Code == "LAYANAN") return current.Name;
            current = parent;
        }

        // Menu dipindah keluar dari LAYANAN: divisi diambil dari kode, mis. LYN_QC_KIRIM_NANAS -> LYN_QC.
        if (Menus.TryGetValue(menuId, out var menu) && menu.Code.StartsWith(ServiceCodePrefix))
        {
            var parts = menu.Code.Split('_');
            if (parts.Length >= 2)
            {
                var divisionCode = $"{parts[0]}_{parts[1]}";
                return Menus.Values.FirstOrDefault(x => x.Code == divisionCode)?.Name;
            }
        }

        return null;
    }
}
