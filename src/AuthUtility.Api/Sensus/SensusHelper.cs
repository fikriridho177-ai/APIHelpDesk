using System.Security.Claims;
using AuthUtility.Application.Interfaces;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Api.Sensus;

// Kode menu sensus (UtlMenu), diatur lewat Hak Akses > Akses Menu.
public static class SnsMenu
{
    public const string Approval = "SNS_APPROVAL";
    public const string Monitoring = "SNS_MONITORING";
    public const string Analisis = "SNS_ANALISIS";
    public const string MasterLokasi = "SNS_MASTER_LOKASI";
    public const string MasterJenis = "SNS_MASTER_JENIS";
    public const string Mobile = "SNS_MOBILE";

    public static readonly string[] All = { Approval, Monitoring, Analisis, MasterLokasi, MasterJenis, Mobile };
}

public static class SnsTingkat
{
    public const string Kosong = "-";
    public const string Nihil = "NIHIL";
    public const string Ringan = "RINGAN";
    public const string Sedang = "SEDANG";
    public const string Berat = "BERAT";

    public static readonly string[] Urutan = { Kosong, Nihil, Ringan, Sedang, Berat };

    // nilai >= AmbangBerat -> BERAT, >= AmbangSedang -> SEDANG, > 0 -> RINGAN, 0 -> NIHIL
    public static string Hitung(decimal? nilai, decimal? ambangSedang, decimal? ambangBerat)
    {
        if (nilai == null) return Kosong;
        if (ambangBerat != null && nilai >= ambangBerat) return Berat;
        if (ambangSedang != null && nilai >= ambangSedang) return Sedang;
        return nilai > 0 ? Ringan : Nihil;
    }

    public static string Max(IEnumerable<string> list) =>
        list.DefaultIfEmpty(Kosong).MaxBy(x => Array.IndexOf(Urutan, x))!;
}

public static class SensusHelper
{
    public static int CurrentUserId(ClaimsPrincipal user) =>
        int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // Boleh membuka data lookup (kebun/blok/jenis) bila punya View/Read di salah satu menu sensus,
    // atau di salah satu halaman sensus per jenis (sub-layanan RNA yang terhubung lewat SnsJenis.MenuCode).
    public static async Task<bool> HasAnyAccessAsync(AppDbContext db, IPermissionService permissions, ClaimsPrincipal user)
    {
        var jenisMenus = await db.SnsJenis.AsNoTracking()
            .Where(x => x.Active && x.MenuCode != null)
            .Select(x => x.MenuCode!)
            .ToListAsync();

        foreach (var code in SnsMenu.All.Concat(jenisMenus).Distinct())
        {
            var p = await permissions.GetPermissionsAsync(user, code);
            if (p != null && (p.CanView || p.CanRead || p.CanCreate)) return true;
        }

        return false;
    }

    // Permission pada halaman sensus per jenis (menu SnsJenis.MenuCode, mis. LYN_RNA_HPT_ULAT_API).
    public static async Task<bool> HasJenisPermissionAsync(
        AppDbContext db, IPermissionService permissions, ClaimsPrincipal user, int jenisId, MenuPermission permission)
    {
        var menuCode = await db.SnsJenis.AsNoTracking()
            .Where(x => x.Id == jenisId)
            .Select(x => x.MenuCode)
            .FirstOrDefaultAsync();

        return !string.IsNullOrWhiteSpace(menuCode) && await permissions.HasPermissionAsync(user, menuCode, permission);
    }

    // Rata-rata parameter indikator per sensus -> tingkat serangan (yang terberat dari semua indikator).
    public static async Task<Dictionary<long, (string Tingkat, decimal? Rata)>> HitungTingkatAsync(
        AppDbContext db, ICollection<long> headerIds)
    {
        if (headerIds.Count == 0) return new();

        var averages = await db.SnsDetails
            .AsNoTracking()
            .Where(d => headerIds.Contains(d.HeaderId) && d.Parameter!.IsIndikator && d.NilaiAngka != null)
            .GroupBy(d => new { d.HeaderId, d.ParameterId, d.Parameter!.AmbangSedang, d.Parameter.AmbangBerat })
            .Select(g => new
            {
                g.Key.HeaderId,
                g.Key.AmbangSedang,
                g.Key.AmbangBerat,
                g.Key.ParameterId,
                Rata = g.Average(x => x.NilaiAngka)
            })
            .ToListAsync();

        return averages
            .GroupBy(x => x.HeaderId)
            .ToDictionary(
                g => g.Key,
                g => (
                    SnsTingkat.Max(g.Select(x => SnsTingkat.Hitung(x.Rata, x.AmbangSedang, x.AmbangBerat))),
                    // Rata indikator utama = indikator dengan Id terkecil (paling awal dibuat).
                    g.OrderBy(x => x.ParameterId).First().Rata));
    }
}
