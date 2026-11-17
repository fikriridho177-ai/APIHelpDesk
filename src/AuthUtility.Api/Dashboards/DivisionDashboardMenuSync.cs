using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Api.Dashboards;

public static class DivisionDashboardMenuSync
{
    public static async Task EnsureAsync(AppDbContext db)
    {
        await using var tx=db.Database.IsRelational()?await db.Database.BeginTransactionAsync():null;
        if(db.Database.ProviderName=="Npgsql.EntityFrameworkCore.PostgreSQL")
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(20261116, 4)");
        foreach(var division in new[]{"GIS","NAT"})
        {
            var code=division+"_DASHBOARD";
            if(await db.Menus.AnyAsync(m=>m.Code==code)) continue;
            var root=await db.Menus.FirstOrDefaultAsync(m=>m.Code=="LYN_"+division && m.Active);
            if(root==null) continue;
            var menu=new UtlMenu {Code=code,Name="Dashboard",ParentId=root.Id,Route="/"+division.ToLowerInvariant()+"/dashboard",
                MenuType="MENU",SortOrder=-1,Active=true,CreatedBy="division-dashboard-sync",CreatedAt=DateTime.UtcNow};
            db.Menus.Add(menu); await db.SaveChangesAsync();
            var rights=await db.GroupMenus.AsNoTracking().Where(r=>r.MenuId==root.Id).ToListAsync();
            db.GroupMenus.AddRange(rights.Select(r=>new UtlGroupMenu {GroupId=r.GroupId,MenuId=menu.Id,CanView=r.CanView,CanRead=r.CanRead}));
            await db.SaveChangesAsync();
        }
        if(tx!=null) await tx.CommitAsync();
    }
}
