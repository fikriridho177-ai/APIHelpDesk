using AuthUtility.Application.Interfaces;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Api.Qc;

// Hak akses user untuk modul QC, dimuat sekali per request.
// Pola sama dengan LayananAccess: Super Admin bypass semua, selain itu cek
// UtlGroupMenu (Hak Akses > Akses Menu). Data scope company/wilayah tidak diterapkan
// di Phase 1 (lihat roadmap); visibility daftar inspeksi = milik sendiri + handler.
public class QcAccess
{
    public const string RootCode = "QC";

    public const string MenuInspeksi = "QC_INSPEKSI";
    public const string MenuBuatInspeksi = "QC_BUAT_INSPEKSI";
    public const string MenuApproval = "QC_APPROVAL";
    public const string MenuFinding = "QC_FINDING";
    public const string MenuMonitoring = "QC_MONITORING";
    public const string MenuReport = "QC_REPORT";

    public int UserId { get; private init; }
    public UtlUser User { get; private init; } = null!;
    public bool IsSuperAdmin { get; private init; }

    // Kode group user — dipakai validasi approver per level (lihat QcApprovalFlowConfig).
    public string? GroupCode { get; private init; }

    private Dictionary<int, UtlMenu> _menus = new();
    private Dictionary<int, UtlGroupMenu> _rights = new();

    public static async Task<QcAccess?> LoadAsync(AppDbContext db, int userId)
    {
        var user = await db.Users
            .AsNoTracking()
            .Include(x => x.Group)
            .FirstOrDefaultAsync(x => x.Id == userId && x.Active);

        if (user?.Group == null || !user.Group.Active) return null;

        var menus = await db.Menus
            .AsNoTracking()
            .Where(x => x.Active && (x.Code == RootCode || x.Code.StartsWith("QC_")))
            .ToDictionaryAsync(x => x.Id);

        var menuIds = menus.Keys.ToList();
        var rights = await db.GroupMenus
            .AsNoTracking()
            .Where(x => x.GroupId == user.GroupId && menuIds.Contains(x.MenuId))
            .ToDictionaryAsync(x => x.MenuId);

        return new QcAccess
        {
            UserId = userId,
            User = user,
            IsSuperAdmin = user.Group.IsSuperAdmin,
            GroupCode = user.Group.Code,
            _menus = menus,
            _rights = rights
        };
    }

    public bool Has(string menuCode, MenuPermission permission)
    {
        var menu = _menus.Values.FirstOrDefault(x => x.Code == menuCode);
        if (menu == null) return false;
        if (IsSuperAdmin) return true;
        if (!_rights.TryGetValue(menu.Id, out var right)) return false;

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

    // Approver level aktif: CanApprove pada menu level + (group cocok bila diisi) atau SuperAdmin.
    public bool CanApproveOn(string menuCode, string? requiredGroupCode)
    {
        if (IsSuperAdmin) return true;
        if (!Has(menuCode, MenuPermission.Approve)) return false;
        return string.IsNullOrWhiteSpace(requiredGroupCode)
            || string.Equals(GroupCode, requiredGroupCode, StringComparison.OrdinalIgnoreCase);
    }

    // Handler = boleh melihat inspeksi orang lain (Read di Inspeksi / Approve di Approval).
    public bool IsHandler =>
        Has(MenuInspeksi, MenuPermission.Read) || Has(MenuApproval, MenuPermission.Approve);

    // CreatedBy disimpan sebagai UserId (login string) akun pembuat.
    public bool IsOwner(QcInspection x) =>
        !string.IsNullOrWhiteSpace(x.CreatedBy) &&
        string.Equals(x.CreatedBy, User.UserId, StringComparison.Ordinal);

    public bool CanSee(QcInspection x) => IsOwner(x) || IsHandler;
}
