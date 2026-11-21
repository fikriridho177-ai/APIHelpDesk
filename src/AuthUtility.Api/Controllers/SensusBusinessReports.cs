using AuthUtility.Api.Sensus;
using AuthUtility.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Api.Controllers;

public partial class SensusController
{
    private Task<bool> BusinessRead()=>CanAny(MenuPermission.Read,SnsMenu.Monitoring,SnsMenu.Analisis,SnsMenu.Dashboard);
    [HttpGet("business-report/lookups")]
    public async Task<IActionResult> BusinessLookups()
    {
        if(!await BusinessRead())return Forbid();
        var wilayah=await db.SnsHeaders.AsNoTracking().Where(h=>h.KodeWilayah!=null).Select(h=>h.KodeWilayah!).Distinct().OrderBy(s=>s).ToListAsync();
        var land=(await db.SnsHeaders.AsNoTracking().Where(h=>h.StatusLahan!=null).Select(h=>h.StatusLahan!).Distinct().ToListAsync())
            .Select(SensusBusinessContext.NormalizeLand).Where(v=>v!=null).Select(v=>v!).Distinct().ToList();
        var kinds=await db.SnsJenis.AsNoTracking().OrderBy(k=>k.Name).Select(k=>new{k.Id,k.Code,k.Name,k.Form2Enabled}).ToListAsync();
        return Ok(new{wilayah,statusLahan=land.Concat(new[]{"ORGANIK","NON_ORGANIK","RAA","BELUM_DIKETAHUI"}).Distinct().OrderBy(v=>v),jenis=kinds});
    }
    [HttpGet("business-report")]
    public async Task<IActionResult> BusinessReport([FromQuery]SensusBusinessFilter filter,CancellationToken ct)=>await ReadBusinessReport(filter,false,ct);
    [HttpGet("business-report/export")]
    public async Task<IActionResult> BusinessExport([FromQuery]SensusBusinessFilter filter,CancellationToken ct)=>await ReadBusinessReport(filter,true,ct);
    [HttpGet("business-report/analysis")]
    public async Task<IActionResult> BusinessAnalysis([FromQuery]SensusBusinessFilter filter,CancellationToken ct)
    {
        var result=await ReadBusinessReport(filter,false,ct);
        return result is OkObjectResult{Value:SnsBusinessReport data}?Ok(new{data.DateFrom,data.DateTo,data.Analysis,data.Notes,total=data.Observations.Count}):result;
    }
    private async Task<IActionResult> ReadBusinessReport(SensusBusinessFilter filter,bool export,CancellationToken ct)
    {
        if(!await BusinessRead() || export&&!await CanAny(MenuPermission.Export,SnsMenu.Monitoring,SnsMenu.Analisis))return Forbid();
        var error=SensusBusinessReportQuery.Validate(filter);if(error!=null)return BadRequest(new{message=error});
        var data=await SensusBusinessReportQuery.ReadAsync(db,filter,ct);
        return data==null?UnprocessableEntity(new{message="Report melebihi 5.000 observasi. Persempit periode atau wilayah."}):Ok(data);
    }
}
