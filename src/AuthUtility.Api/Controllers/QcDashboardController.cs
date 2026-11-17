using System.Security.Claims;
using AuthUtility.Api.Dashboards;
using AuthUtility.Api.Qc;
using AuthUtility.Application.Interfaces;
using AuthUtility.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/qc/dashboard")]
public class QcDashboardController(AppDbContext db):ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(DateOnly? dateFrom,DateOnly? dateTo,CancellationToken ct=default)
    {
        var id=User.FindFirstValue(ClaimTypes.NameIdentifier);
        if(!int.TryParse(id,out var userId))return Unauthorized();
        var access=await QcAccess.LoadAsync(db,userId);
        if(access==null || !access.Has("QC_DASHBOARD",MenuPermission.View) || !access.Has("QC_DASHBOARD",MenuPermission.Read))return Forbid();
        var today=DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var from=dateFrom??today.AddDays(-179);var to=dateTo??today;
        if(from>to || to.DayNumber-from.DayNumber>=366)return BadRequest(new{message="Periode dashboard maksimal 366 hari dan tanggal harus berurutan."});
        var query=db.QcInspections.AsNoTracking().Where(i=>i.InspectionDate>=from && i.InspectionDate<=to);
        // Same owner/handler scope as the existing QC inspection/finding lists.
        if(!access.IsHandler)query=query.Where(i=>i.CreatedBy==access.User.UserId);
        var inspections=await query.Select(i=>new QcDashboardInspection(i.Id,i.InspectionNo,i.Form==null?"":i.Form.Code,
            i.Form==null?"Form tidak tersedia":i.Form.Name,i.InspectionDate,i.WilayahKode,i.Status)).ToListAsync(ct);
        var findings=await db.QcFindings.AsNoTracking().Where(f=>query.Any(i=>i.Id==f.InspectionId))
            .Select(f=>new QcDashboardFinding(f.InspectionId,f.Status,f.Severity,f.TargetDate)).ToListAsync(ct);
        return Ok(QcDashboardCalculator.Build(inspections,findings,from,to,today));
    }
}
