using System.Security.Claims;
using System.Text.Json;
using AuthUtility.Api.Controllers;
using AuthUtility.Api.Qc;
using AuthUtility.Application.DTOs.Qc;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;

namespace AuthUtility.Tests;

public class QcApprovalFlowTests
{
    private static IConfiguration Settings() => new ConfigurationBuilder()
        .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: false).Build();

    [Theory]
    [InlineData("QC-PANEN-KLP")]
    [InlineData("QC-PUPUK-TABUR-NNS")]
    [InlineData("QC-KEBERSIHAN-NNS")]
    public void ConfigDefinesThreeOrderedStagesWithLabelsAndRoles(string formCode)
    {
        var flow = new QcApprovalFlowConfig(Settings());
        var levels = flow.GetLevels(formCode);
        Assert.Equal(new[] { 1, 2, 3 }, levels.Select(x => x.Level));
        Assert.Equal(new[] { "DIPERIKSA OLEH", "APPROVED OLEH", "DIKETAHUI OLEH" }, levels.Select(x => x.Label));
        Assert.All(levels, x => { Assert.Equal("QC_APPROVAL", x.MenuCode); Assert.False(string.IsNullOrWhiteSpace(x.RoleName)); });
        Assert.Equal(3, flow.MaxLevel(formCode));
    }

    [Theory]
    [InlineData("QC-PANEN-KLP")]
    [InlineData("QC-PUPUK-TABUR-NNS")]
    [InlineData("QC-KEBERSIHAN-NNS")]
    public async Task ExistingControllerApprovesOnlyAfterLevelThree(string formCode)
    {
        await using var db = await Database(formCode);
        var flow = TestFlow(formCode);
        var controller = Controller(db, flow);
        Assert.IsType<OkObjectResult>(await controller.Submit(1, default));
        Assert.Equal(1, db.QcInspections.Single().CurrentApprovalLevel);
        Assert.Single(db.QcApprovals);
        for (var level = 1; level <= 3; level++)
        {
            Assert.IsType<OkObjectResult>(await controller.Approve(1, null, default));
            var inspection = db.QcInspections.Single();
            Assert.Equal(level < 3 ? QcInspectionStatus.WaitingApproval : QcInspectionStatus.Approved, inspection.Status);
            Assert.Equal(level < 3 ? level + 1 : 3, inspection.CurrentApprovalLevel);
            if (level < 3) Assert.Null(inspection.ApprovedAt);
            else Assert.NotNull(inspection.ApprovedAt);
        }
        Assert.Equal("reporter", db.QcInspections.Single().CreatedBy);
        Assert.Equal(new[] { 1, 2, 3 }, db.QcApprovals.OrderBy(x => x.Level).Select(x => x.Level));
        Assert.All(db.QcApprovals, x => Assert.Equal(QcApprovalStatus.Approved, x.Status));
    }

    [Fact]
    public async Task RevisionAndResubmitAtLevelTwoRetainsFirstApproval()
    {
        const string code = "QC-PANEN-KLP";
        await using var db = await Database(code);
        var controller = Controller(db, TestFlow(code));
        await controller.Submit(1, default);
        await controller.Approve(1, null, default);
        Assert.IsType<OkObjectResult>(await controller.RequestRevision(1, new() { Comment = "Perbaiki data" }, default));
        Assert.Equal(2, db.QcInspections.Single().CurrentApprovalLevel);
        Assert.Equal(QcInspectionStatus.RevisionRequired, db.QcInspections.Single().Status);
        Assert.IsType<OkObjectResult>(await controller.Resubmit(1, null, default));
        Assert.Equal(2, db.QcInspections.Single().CurrentApprovalLevel);
        Assert.Equal(QcApprovalStatus.Approved, db.QcApprovals.Single(x => x.Level == 1).Status);
        var pending = db.QcApprovals.Single(x => x.Level == 2);
        Assert.Equal(QcApprovalStatus.Pending, pending.Status);
        Assert.Null(pending.ApproverUserId);
        Assert.Null(pending.ApproverNameSnapshot);
        Assert.Null(pending.ApprovedAt);
        Assert.Null(pending.Comment);
        await controller.Approve(1, null, default);
        Assert.Equal(QcInspectionStatus.WaitingApproval, db.QcInspections.Single().Status);
        Assert.Equal(3, db.QcInspections.Single().CurrentApprovalLevel);
    }

    [Theory]
    [InlineData(true, "QC_CHECKER", "QC_CHECKER", false, true)]
    [InlineData(true, "QC_CHECKER", "QC_MANAGER", false, false)]
    [InlineData(false, "QC_CHECKER", "QC_CHECKER", false, false)]
    [InlineData(true, "QC_CHECKER", null, false, true)]
    [InlineData(false, "QC_CHECKER", "QC_MANAGER", true, true)]
    public async Task EligibilityKeepsExistingPermissionGroupAndSuperAdminRules(bool canApprove, string groupCode, string? required, bool superAdmin, bool expected)
    {
        await using var db = await Database("QC-PANEN-KLP");
        db.Groups.Single().Code = groupCode;
        db.Groups.Single().IsSuperAdmin = superAdmin;
        db.GroupMenus.Single().CanApprove = canApprove;
        await db.SaveChangesAsync();
        var access = await QcAccess.LoadAsync(db, 1);
        Assert.Equal(expected, access!.CanApproveOn("QC_APPROVAL", required));
    }

    [Fact]
    public async Task QueueAndFlowEndpointExposeStageMetadata()
    {
        const string code = "QC-KEBERSIHAN-NNS";
        await using var db = await Database(code);
        var flow = TestFlow(code);
        await Controller(db, flow).Submit(1, default);
        var approvals = new QcApprovalsController(db, flow) { ControllerContext = Context() };
        var result = Assert.IsType<OkObjectResult>(await approvals.GetPending(default));
        var row = Assert.Single(Assert.IsAssignableFrom<IEnumerable<QcPendingApprovalDto>>(result.Value));
        Assert.Equal(3, row.MaxApprovalLevel);
        Assert.Equal("DIPERIKSA OLEH", row.ApprovalLabel);
        Assert.False(string.IsNullOrWhiteSpace(row.ApprovalRoleName));
        var metadata = Assert.IsType<OkObjectResult>(await approvals.GetFlow(code, default));
        var json = JsonSerializer.SerializeToElement(metadata.Value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.Equal(3, json.GetProperty("maxLevel").GetInt32());
        Assert.Equal(3, json.GetProperty("levels").GetArrayLength());
        Assert.Equal(code, json.GetProperty("formCode").GetString());
    }

    private static QcApprovalFlowConfig TestFlow(string code) => new(new ConfigurationBuilder().AddConfiguration(Settings())
        .AddInMemoryCollection(Enumerable.Range(0, 3).Select(index => new KeyValuePair<string, string?>($"Qc:ApprovalFlows:{code}:Levels:{index}:GroupCode", ""))).Build());

    private static ControllerContext Context() => new() { HttpContext = new DefaultHttpContext
    {
        User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "1") }, "test"))
    } };

    private static QcInspectionsController Controller(AppDbContext db, QcApprovalFlowConfig flow) => new(db, new QcStatusEngine(), new QcNumberGenerator(Settings()), flow)
        { ControllerContext = Context() };

    private static async Task<AppDbContext> Database(string code)
    {
        // Synthetic test groups only; no live database or production groups are written.
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options;
        var db = new AppDbContext(options);
        var group = new UtlGroup { Id = 1, Code = "QC_CHECKER", Name = "Test QC", Active = true };
        var menu = new UtlMenu { Id = 1, Code = "QC_APPROVAL", Name = "QC Approval", Active = true };
        db.Groups.Add(group); db.Menus.Add(menu);
        db.Users.Add(new UtlUser { Id = 1, UserId = "reporter", Name = "Test Reporter", PasswordHash = "test", GroupId = 1, Group = group, Active = true });
        db.GroupMenus.Add(new UtlGroupMenu { GroupId = 1, MenuId = 1, CanApprove = true });
        var form = new QcForm { Id = 1, Code = code, Name = code, Commodity = code.EndsWith("KLP") ? "KELAPA" : "NANAS" };
        db.QcForms.Add(form);
        db.QcInspections.Add(new QcInspection { Id = 1, FormId = 1, Form = form, CreatedBy = "reporter", Commodity = form.Commodity,
            Status = QcInspectionStatus.Draft, InspectionDate = new DateOnly(2026, 11, 13) });
        await db.SaveChangesAsync();
        return db;
    }
}
