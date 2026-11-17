using AuthUtility.Api.Dashboards;
using AuthUtility.Api.Layanan;
using AuthUtility.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Api.Controllers;

public partial class LayananController
{
    [HttpGet("~/api/divisi/{division}/dashboard")]
    public async Task<IActionResult> GetDivisionDashboard(string division, DateOnly? dateFrom, DateOnly? dateTo,
        [FromServices] IPermissionService permissions)
    {
        division=division.ToLowerInvariant();
        if(division is not ("gis" or "nat")) return NotFound();
        var dashboardCode=division.ToUpperInvariant()+"_DASHBOARD";
        if(!await permissions.HasPermissionAsync(User,dashboardCode,MenuPermission.View) ||
            !await permissions.HasPermissionAsync(User,dashboardCode,MenuPermission.Read)) return Forbid();
        var today=DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var from=dateFrom??today.AddDays(-179); var to=dateTo??today;
        if(from>to || to.DayNumber-from.DayNumber>=366 || from==DateOnly.MinValue || to==DateOnly.MaxValue)
            return BadRequest(new {message="Periode dashboard harus berurutan dan maksimal 366 hari."});
        var access=await LayananAccess.LoadAsync(db,CurrentUserId);
        if(access==null) return Forbid();
        var divisionCode="LYN_"+division.ToUpperInvariant();
        var root=access.Menus.Values.FirstOrDefault(m=>m.Code==divisionCode);
        if(root==null) return NotFound(new {message="Divisi tidak tersedia atau tidak aktif."});
        var ids=access.Menus.Values.Where(m=>access.GetDivision(m.Id)?.Id==root.Id || m.Id==root.Id).Select(m=>m.Id).ToList();
        var startUtc=DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue).AddHours(-7),DateTimeKind.Utc);
        var endUtc=DateTime.SpecifyKind(to.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(-7),DateTimeKind.Utc);
        var records=await QueryVisible(access,false,ids,startUtc,endUtc);
        var kinds=await db.LynJenisPermintaan.AsNoTracking().Where(k=>k.Active && k.DivisionMenuId==root.Id)
            .OrderBy(k=>k.SortOrder).ThenBy(k=>k.Name).Select(k=>new DashboardKind(k.Id,k.Code,k.Name)).ToListAsync();
        var rows=records.Select(r=>new ReportRow(r,root.Name,JenisName(r,access,withOther:false)));
        return Ok(DivisionDashboardCalculator.Build(division,root.Name,from,to,rows,kinds,today));
    }
}
