using System.Data;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Api.Sensus;

// Menu metadata only. Existing transaction/master/approval tables are untouched.
public static class SensusWebMenuSync
{
    public static async Task<bool> EnsureAsync(AppDbContext db)
    {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted) : null;
        if (db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(20261116, 3)");
        var menus = await db.Menus.ToListAsync();
        UtlMenu? Find(string code) => menus.FirstOrDefault(m => m.Code == code);
        var initial = Find("SNS_DASHBOARD") == null;
        var changed = false;
        async Task<UtlMenu> Add(string code, string name, string? route, int? parent, string type, int order)
        {
            var menu = new UtlMenu { Code=code,Name=name,Route=route,ParentId=parent,MenuType=type,SortOrder=order,Active=true,CreatedBy="sensus-web-menu-sync",CreatedAt=DateTime.UtcNow };
            db.Menus.Add(menu); menus.Add(menu); await db.SaveChangesAsync(); changed=true; return menu;
        }
        var layanan = Find("LAYANAN") ?? await Add("LAYANAN","Layanan","/layanan",null,"GROUP",10);
        var rna = Find("LYN_RNA") ?? Find("SNS") ?? await Add("LYN_RNA","RNA","/sensus/dashboard",layanan.Id,"GROUP",10);
        if (initial) { rna.ParentId=layanan.Id; rna.Name="RNA"; rna.MenuType="GROUP"; rna.Route="/sensus/dashboard"; changed=true; }
        var definitions = new[] {
            (Code:"SNS_DASHBOARD",Name:"Dashboard",Route:"/sensus/dashboard",Order:0),
            (Code:"SNS_MOBILE",Name:"Input Sensus",Route:"/sensus/input",Order:1),
            (Code:"SNS_APPROVAL",Name:"Approval",Route:"/sensus/approval",Order:2),
            (Code:"SNS_MONITORING",Name:"Monitoring & Analisis",Route:"/sensus/monitoring",Order:3)
        };
        foreach (var d in definitions)
        {
            var menu = Find(d.Code) ?? await Add(d.Code,d.Name,d.Route,rna.Id,"MENU",d.Order);
            if (initial) { menu.Name=d.Name; menu.Route=d.Route; menu.ParentId=rna.Id; menu.MenuType="MENU"; menu.SortOrder=d.Order; }
        }
        // Initial conversion only: subsequent starts preserve Management Menu edits and Active=false.
        if (initial)
        {
            var rights = await db.GroupMenus.ToListAsync();
            var dashboard = Find("SNS_DASHBOARD")!;
            var monitoring = Find("SNS_MONITORING")!;
            var oldMonitoringRights = rights.Where(r=>r.MenuId==monitoring.Id).ToList();
            foreach (var right in oldMonitoringRights)
                if (!rights.Any(r=>r.GroupId==right.GroupId && r.MenuId==dashboard.Id))
                    db.GroupMenus.Add(new() { GroupId=right.GroupId,MenuId=dashboard.Id,CanView=right.CanView,CanRead=right.CanRead,CanExport=right.CanExport });
            var legacyIds = menus.Where(m=>m.Code is "SNS_ANALISIS" or "SNS_REPORT").Select(m=>m.Id).ToHashSet();
            foreach (var group in rights.Where(r=>legacyIds.Contains(r.MenuId)).GroupBy(r=>r.GroupId))
            {
                var target=rights.FirstOrDefault(r=>r.GroupId==group.Key && r.MenuId==monitoring.Id);
                if (target==null) { target=new() {GroupId=group.Key,MenuId=monitoring.Id}; db.GroupMenus.Add(target); rights.Add(target); }
                target.CanView |= group.Any(r=>r.CanView); target.CanRead |= group.Any(r=>r.CanRead); target.CanExport |= group.Any(r=>r.CanExport);
            }
            var hptCodes=(await db.SnsJenis.AsNoTracking().Where(h=>h.MenuCode!=null).Select(h=>h.MenuCode!).ToListAsync()).ToHashSet();
            var retained=definitions.Select(d=>d.Code).Concat(new[]{rna.Code,"SNS_MASTER_JENIS","SNS_MASTER_LOKASI"}).ToHashSet();
            foreach(var menu in menus.Where(m=>!retained.Contains(m.Code) &&
                (m.Code.StartsWith("LYN_RNA_") || legacyIds.Contains(m.Id) || hptCodes.Contains(m.Code)))) menu.Active=false;
            var master=menus.FirstOrDefault(m=>m.Active && (m.Code is "MASTER" or "MASTER_DATA" or "UTILITY_MASTER" || m.MenuType=="GROUP" && m.Name.Equals("Master",StringComparison.OrdinalIgnoreCase)));
            foreach(var menu in menus.Where(m=>m.ParentId==rna.Id && m.Code.StartsWith("SNS_MASTER_"))) menu.ParentId=master?.Id;
            await db.SaveChangesAsync();
        }
        if(transaction!=null) await transaction.CommitAsync();
        return changed;
    }
}
