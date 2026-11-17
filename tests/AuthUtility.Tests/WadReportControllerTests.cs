using System.Security.Claims;
using AuthUtility.Api.Controllers;
using AuthUtility.Api.Wad;
using AuthUtility.Application.DTOs.Security;
using AuthUtility.Application.Interfaces;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AuthUtility.Tests;

public class WadReportControllerTests
{
    private static AppDbContext Database() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static WadReportsController Controller(AppDbContext db, bool read = true, bool create = true, bool update = true, bool admin = false)
    {
        return new WadReportsController(db, new TestPermissions(read, create, update, admin), new ConfigurationBuilder().Build(), null!, NullLogger<WadReportsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "17") }, "test"))
            } }
        };
    }

    private static WadObject Object(int id, string module) => new()
    {
        Id = id, Module = module, Code = $"TEST-{id}", Name = "Test object", Wilayah = "Test region", ObjectType = "PROYEK", Active = true, TargetQty = 100, Unit = "m3"
    };

    private static WadReport Report(long id, WadObject obj, string date, int percent = 0, int creator = 17) => new()
    {
        Id = id, ObjectId = obj.Id, Object = obj, Module = obj.Module,
        ObservedDate = DateOnly.Parse(date), ProgressPercent = percent, CreatedByUserId = creator,
        CreatedByName = "Test author", CreatedAt = DateTime.UtcNow, WorkDescription = "Test work"
    };

    [Fact]
    public async Task Summary_UsesLatestObservation_NotInputId_AndDoesNotSumHistory()
    {
        await using var db = Database();
        var obj = Object(1, "PROJECT");
        db.WadObjects.Add(obj);
        db.WadReports.AddRange(Report(1, obj, "2026-11-15", 60), Report(2, obj, "2026-11-10", 20));
        await db.SaveChangesAsync();
        var result = Assert.IsType<OkObjectResult>(await Controller(db).GetSummary("PROJECT", null, null, null, null, null, null, default));
        var summary = Assert.IsType<WadSummaryDto>(result.Value);
        Assert.Equal(2, summary.TotalReports);
        Assert.Equal(1, summary.UniqueObjects);
        Assert.Equal(60, summary.Project!.AverageProgress);
    }

    [Fact]
    public async Task Summary_ConditionDoesNotResurrectOldFloodState()
    {
        await using var db = Database();
        var obj = Object(1, "FLOOD");
        var old = Report(1, obj, "2026-11-10"); old.Condition = "MELUAS"; old.FloodAreaHa = 3;
        var latest = Report(2, obj, "2026-11-15"); latest.Condition = "SURUT"; latest.FloodAreaHa = 0;
        db.WadObjects.Add(obj); db.WadReports.AddRange(old, latest); await db.SaveChangesAsync();
        var result = Assert.IsType<OkObjectResult>(await Controller(db).GetSummary("FLOOD", null, null, null, null, "MELUAS", null, default));
        var summary = Assert.IsType<WadSummaryDto>(result.Value);
        Assert.Equal(1, summary.TotalReports);
        Assert.Equal(0, summary.Flood!.FloodedParcels);
        Assert.Equal(0, summary.Flood.TotalFloodAreaHa);
    }

    [Fact]
    public async Task EditingBackdatedProjectRecalculatesLatestSummary()
    {
        await using var db = Database();
        var obj = Object(1, "PROJECT");
        db.WadObjects.Add(obj); db.WadReports.AddRange(Report(1, obj, "2026-11-15", 60), Report(2, obj, "2026-11-10", 20)); await db.SaveChangesAsync();
        var controller = Controller(db);
        Assert.IsType<OkObjectResult>(await controller.UpdateReport(2, new WadReportInput
        { ObjectId = 1, ObservedDate = new DateOnly(2026, 11, 16), WorkDescription = "Updated test work", RealizationQty = 80 }, default));
        var result = Assert.IsType<OkObjectResult>(await controller.GetSummary("PROJECT", null, null, null, 1, null, null, default));
        Assert.Equal(80, Assert.IsType<WadSummaryDto>(result.Value).Project!.AverageProgress);
    }

    [Fact]
    public async Task PeatEdit_PreservesStoredReferenceAndMethod()
    {
        await using var db = Database();
        var obj = Object(1, "PEAT"); obj.ReferenceValueCm = 500; obj.MeasurementMethod = "EXPOSED_ROD";
        var report = Report(1, obj, "2026-11-10"); report.ReferenceValueCm = 100; report.MeasurementMethod = "ELEVATION";
        db.WadObjects.Add(obj); db.WadReports.Add(report); await db.SaveChangesAsync();
        var result = Assert.IsType<OkObjectResult>(await Controller(db).UpdateReport(1, new WadReportInput
        { ObjectId = 1, ObservedDate = report.ObservedDate, MeasurementValue = 97, MeasurementUnit = "CM" }, default));
        var dto = Assert.IsType<WadReportDto>(result.Value);
        Assert.Equal(100m, dto.ReferenceValueCm); Assert.Equal("ELEVATION", dto.MeasurementMethod); Assert.Equal(3m, dto.DropCm);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task Detail_EditRequiresOwnershipOrAdmin(bool admin, bool editable)
    {
        await using var db = Database(); var obj = Object(1, "PROJECT");
        db.WadObjects.Add(obj); db.WadReports.Add(Report(1, obj, "2026-11-10", creator: 99)); await db.SaveChangesAsync();
        var result = Assert.IsType<OkObjectResult>(await Controller(db, admin: admin).GetReport(1, default));
        Assert.Equal(editable, Assert.IsType<WadReportDto>(result.Value).CanEdit);
    }

    [Fact]
    public async Task Update_OtherUsersReport_IsForbidden()
    {
        await using var db = Database(); var obj = Object(1, "PROJECT");
        db.WadObjects.Add(obj); db.WadReports.Add(Report(1, obj, "2026-11-10", creator: 99)); await db.SaveChangesAsync();
        Assert.IsType<ForbidResult>(await Controller(db).UpdateReport(1, new WadReportInput(), default));
    }

    [Fact]
    public async Task DeniedPermissions_StopBeforeDatabaseAndFileAccess()
    {
        await using var db = Database(); var controller = Controller(db, read: false, create: false, update: false);
        Assert.IsType<ForbidResult>(await controller.GetObjects("WATER", null, default));
        Assert.IsType<ForbidResult>(await controller.GetReport(1, default));
        Assert.IsType<ForbidResult>(await controller.GetPhoto(1, default));
        Assert.IsType<ForbidResult>(await controller.CreateReport(new WadReportInput(), default));
        Assert.IsType<ForbidResult>(await controller.UpdateReport(1, new WadReportInput(), default));
        Assert.IsType<ForbidResult>(await controller.UploadPhoto(1, null!, null, null, Guid.NewGuid(), default));
        Assert.IsType<ForbidResult>(await controller.DeletePhoto(1, 1, default));
    }

    private sealed class TestPermissions(bool read, bool create, bool update, bool admin) : IPermissionService
    {
        public Task<bool> IsSuperAdminAsync(ClaimsPrincipal principal) => Task.FromResult(admin);
        public Task<bool> HasPermissionAsync(ClaimsPrincipal principal, string menuCode, MenuPermission permission) => Task.FromResult(admin || permission switch
        { MenuPermission.Read => read, MenuPermission.Create => create, MenuPermission.Update => update, _ => false });
        public Task<CurrentMenuPermissionDto?> GetPermissionsAsync(ClaimsPrincipal principal, string menuCode) => Task.FromResult<CurrentMenuPermissionDto?>(null);
    }
}
