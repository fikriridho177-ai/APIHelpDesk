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

// Fase 2: alur approval digerakkan master MstQcFormApprovalStep (QcApprovalFlow),
// bukan appsettings. Langkah pembuat (IsCreatorStep) otomatis saat submit;
// revisi membuat siklus Revision baru tanpa menghapus baris lama.
public class QcApprovalFlowTests
{
    private static IConfiguration Settings() => new ConfigurationBuilder()
        .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: false).Build();

    [Fact]
    public async Task MasterFourLevelsCreatorAutoApprovedThenTieredApproval()
    {
        await using var db = await Database(withSteps: true);
        var controller = Controller(db);

        Assert.IsType<OkObjectResult>(await controller.Submit(1, default));
        var inspection = db.QcInspections.Single();
        Assert.Equal(QcInspectionStatus.Submitted, inspection.Status);
        Assert.Equal(2, inspection.CurrentApprovalLevel);
        Assert.Equal(0, inspection.Revision);
        Assert.NotNull(inspection.SubmittedAt);

        // Baris pembuat otomatis APPROVED atas nama pembuat; level 2 PENDING.
        var creator = db.QcApprovals.Single(x => x.Level == 1);
        Assert.Equal(QcApprovalStatus.Approved, creator.Status);
        Assert.Equal(QcApprovalAction.Approve, creator.Action);
        Assert.Equal("Test Reporter", creator.ApproverNameSnapshot);
        Assert.NotNull(creator.ApprovedAt);
        Assert.Equal(QcApprovalStatus.Pending, db.QcApprovals.Single(x => x.Level == 2).Status);

        Assert.IsType<OkObjectResult>(await controller.Approve(1, null, default));
        Assert.Equal(QcInspectionStatus.InReview, db.QcInspections.Single().Status);
        Assert.Equal(3, db.QcInspections.Single().CurrentApprovalLevel);

        Assert.IsType<OkObjectResult>(await controller.Approve(1, null, default));
        Assert.Equal(QcInspectionStatus.InReview, db.QcInspections.Single().Status);
        Assert.Equal(4, db.QcInspections.Single().CurrentApprovalLevel);
        Assert.Null(db.QcInspections.Single().ApprovedAt);

        Assert.IsType<OkObjectResult>(await controller.Approve(1, null, default));
        Assert.Equal(QcInspectionStatus.Approved, db.QcInspections.Single().Status);
        Assert.NotNull(db.QcInspections.Single().ApprovedAt);
        Assert.Equal(4, db.QcApprovals.Count());
        Assert.All(db.QcApprovals, x => Assert.Equal(QcApprovalStatus.Approved, x.Status));
    }

    [Fact]
    public async Task FormWithoutStepsFallsBackToSingleManualApproval()
    {
        await using var db = await Database(withSteps: false);
        var controller = Controller(db);

        Assert.IsType<OkObjectResult>(await controller.Submit(1, default));
        Assert.Equal(QcInspectionStatus.Submitted, db.QcInspections.Single().Status);
        Assert.Equal(1, db.QcInspections.Single().CurrentApprovalLevel);
        var row = Assert.Single(db.QcApprovals);
        Assert.Equal(QcApprovalStatus.Pending, row.Status);

        Assert.IsType<OkObjectResult>(await controller.Approve(1, null, default));
        Assert.Equal(QcInspectionStatus.Approved, db.QcInspections.Single().Status);
    }

    [Fact]
    public async Task CreatorOnlyFlowApprovesImmediatelyOnSubmit()
    {
        await using var db = await Database(withSteps: false);
        db.QcFormApprovalSteps.Add(new QcFormApprovalStep
        {
            FormId = 1, Level = 1, Label = "Dilaporkan", MenuCode = "QC_APPROVAL", IsCreatorStep = true, Active = true
        });
        await db.SaveChangesAsync();

        Assert.IsType<OkObjectResult>(await Controller(db).Submit(1, default));
        Assert.Equal(QcInspectionStatus.Approved, db.QcInspections.Single().Status);
        Assert.NotNull(db.QcInspections.Single().ApprovedAt);
        Assert.Equal(QcApprovalStatus.Approved, Assert.Single(db.QcApprovals).Status);
    }

    [Fact]
    public async Task RevisionRequiresCommentAndResubmitStartsNewRevisionCycle()
    {
        await using var db = await Database(withSteps: true);
        var controller = Controller(db);
        await controller.Submit(1, default);
        await controller.Approve(1, null, default); // level 2 -> 3

        // Catatan wajib untuk request-revision.
        Assert.IsType<BadRequestObjectResult>(await controller.RequestRevision(1, new(), default));
        Assert.IsType<OkObjectResult>(await controller.RequestRevision(1, new() { Comment = "Perbaiki data sampel" }, default));
        Assert.Equal(QcInspectionStatus.Revision, db.QcInspections.Single().Status);
        Assert.Equal(3, db.QcInspections.Single().CurrentApprovalLevel);
        Assert.Equal(QcApprovalStatus.RevisionRequired, db.QcApprovals.Single(x => x.Level == 3 && x.Revision == 0).Status);

        // Edit saat REVISION diizinkan, lalu resubmit.
        Assert.IsType<OkObjectResult>(await controller.UpdateInspection(1, new UpdateQcInspectionRequest { NoIcarLama = "ICAR-001" }, default));
        Assert.IsType<OkObjectResult>(await controller.Resubmit(1, new() { Comment = "Sudah diperbaiki" }, default));

        var inspection = db.QcInspections.Single();
        Assert.Equal(1, inspection.Revision);
        Assert.Equal(QcInspectionStatus.Submitted, inspection.Status);
        Assert.Equal(3, inspection.CurrentApprovalLevel);

        // Baris revisi 0 tetap tersimpan; siklus baru punya pembuat + PENDING level 3.
        Assert.Equal(QcApprovalStatus.Approved, db.QcApprovals.Single(x => x.Level == 2 && x.Revision == 0).Status);
        Assert.Equal(QcApprovalStatus.RevisionRequired, db.QcApprovals.Single(x => x.Level == 3 && x.Revision == 0).Status);
        Assert.Equal(QcApprovalStatus.Approved, db.QcApprovals.Single(x => x.Level == 1 && x.Revision == 1).Status);
        var pending = db.QcApprovals.Single(x => x.Level == 3 && x.Revision == 1);
        Assert.Equal(QcApprovalStatus.Pending, pending.Status);
        Assert.Null(pending.ApproverUserId);
        Assert.Null(pending.ApprovedAt);

        await controller.Approve(1, null, default);
        Assert.Equal(QcInspectionStatus.InReview, db.QcInspections.Single().Status);
        Assert.Equal(4, db.QcInspections.Single().CurrentApprovalLevel);
        await controller.Approve(1, null, default);
        Assert.Equal(QcInspectionStatus.Approved, db.QcInspections.Single().Status);
        Assert.All(db.QcApprovals.Where(x => x.Revision == 1), x => Assert.Equal(QcApprovalStatus.Approved, x.Status));
    }

    [Theory]
    [InlineData(true, 0, false, true)]   // hak Approve menu step (group tidak dipetakan)
    [InlineData(false, 1, false, true)]  // anggota group approver step
    [InlineData(false, 2, false, false)] // bukan group & tanpa hak approve
    [InlineData(false, 0, true, true)]   // SuperAdmin bypass
    public async Task EligibilityUsesStepGroupOrMenuApproveRight(bool canApprove, int stepGroupId, bool superAdmin, bool expected)
    {
        await using var db = await Database(withSteps: true);
        db.Groups.Add(new UtlGroup { Id = 2, Code = "QC_AFD", Name = "Approver Afdeling", Active = true });
        db.GroupMenus.Single().CanApprove = canApprove;
        db.Groups.Single(x => x.Id == 1).IsSuperAdmin = superAdmin;
        var step2 = db.QcFormApprovalSteps.Single(x => x.Level == 2);
        step2.ApproverGroupId = stepGroupId == 0 ? null : stepGroupId;
        await db.SaveChangesAsync();

        var access = await QcAccess.LoadAsync(db, 1);
        var step = (await new QcApprovalFlow(db).GetStepsAsync(1)).Single(x => x.Level == 2);
        Assert.Equal(expected, access!.CanApproveStep(step));
    }

    [Fact]
    public async Task AfdelingScopeBlocksOtherDeptButAllowsMatchAndUnmappedDept()
    {
        await using var db = await Database(withSteps: true);
        db.QcInspections.Single().KodeAfdeling = "AFD2";
        db.Users.Single().DeptId = "AFD1";
        await db.SaveChangesAsync();
        var controller = Controller(db);
        await controller.Submit(1, default);

        // Level 2 ScopeByAfdeling=true: DeptId beda afdeling -> 403.
        var blocked = await controller.Approve(1, null, default);
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(blocked).StatusCode);

        // DeptId cocok (case-insensitive, trim) -> boleh.
        db.Users.Single().DeptId = " afd2 ";
        await db.SaveChangesAsync();
        Assert.IsType<OkObjectResult>(await Controller(db).Approve(1, null, default));

        // DeptId kosong = belum dipetakan -> sementara unscoped.
        await using var db2 = await Database(withSteps: true);
        db2.QcInspections.Single().KodeAfdeling = "AFD2";
        await db2.SaveChangesAsync();
        var c2 = Controller(db2);
        await c2.Submit(1, default);
        Assert.IsType<OkObjectResult>(await c2.Approve(1, null, default));
    }

    [Fact]
    public async Task QueueAndFlowEndpointExposeMasterStageMetadata()
    {
        await using var db = await Database(withSteps: true);
        await Controller(db).Submit(1, default);
        var approvals = new QcApprovalsController(db, new QcApprovalFlow(db)) { ControllerContext = Context() };

        var result = Assert.IsType<OkObjectResult>(await approvals.GetPending(default));
        var row = Assert.Single(Assert.IsAssignableFrom<IEnumerable<QcPendingApprovalDto>>(result.Value));
        Assert.Equal(4, row.MaxApprovalLevel);
        Assert.Equal(2, row.CurrentApprovalLevel);
        Assert.Equal(0, row.Revision);
        Assert.Equal("P. Jawab Afd", row.ApprovalLabel);
        Assert.Equal("ASISTEN_WILAYAH", row.ApprovalRoleName);
        Assert.True(row.ScopeByAfdeling);

        var metadata = Assert.IsType<OkObjectResult>(await approvals.GetFlow("QC-PANEN-KLP", default));
        var json = JsonSerializer.SerializeToElement(metadata.Value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.Equal(4, json.GetProperty("maxLevel").GetInt32());
        Assert.Equal(4, json.GetProperty("levels").GetArrayLength());
        Assert.True(json.GetProperty("levels")[0].GetProperty("isCreatorStep").GetBoolean());
        Assert.False(json.GetProperty("levels")[1].GetProperty("isCreatorStep").GetBoolean());
        Assert.Equal("QC-PANEN-KLP", json.GetProperty("formCode").GetString());
    }

    [Fact]
    public async Task ApproveRejectsWhenNotPendingOrNotCreatorEditable()
    {
        await using var db = await Database(withSteps: true);
        var controller = Controller(db);
        // DRAFT tidak bisa di-approve.
        Assert.IsType<BadRequestObjectResult>(await controller.Approve(1, null, default));
        await controller.Submit(1, default);
        await controller.Approve(1, null, default);
        await controller.Approve(1, null, default);
        await controller.Approve(1, null, default);
        // APPROVED tidak bisa di-approve lagi.
        Assert.IsType<BadRequestObjectResult>(await controller.Approve(1, null, default));
        // APPROVED terkunci dari edit.
        Assert.IsType<BadRequestObjectResult>(await controller.UpdateInspection(1, new UpdateQcInspectionRequest { NoIcarLama = "X" }, default));
    }

    private static ControllerContext Context() => new() { HttpContext = new DefaultHttpContext
    {
        User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "1") }, "test"))
    } };

    private static QcInspectionsController Controller(AppDbContext db) =>
        new(db, new QcStatusEngine(), new QcNumberGenerator(Settings(), new InMemoryQcNumberSeqStore()), new QcApprovalFlow(db))
        { ControllerContext = Context() };

    private static async Task<AppDbContext> Database(bool withSteps)
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
        var form = new QcForm { Id = 1, Code = "QC-PANEN-KLP", Name = "Panen Kelapa", Commodity = "KELAPA" };
        db.QcForms.Add(form);
        if (withSteps)
        {
            db.QcFormApprovalSteps.AddRange(
                new QcFormApprovalStep { FormId = 1, Level = 1, Label = "Dilaporkan", JabatanKode = "INSPEKTUR_QC", MenuCode = "QC_APPROVAL", IsCreatorStep = true, Active = true },
                new QcFormApprovalStep { FormId = 1, Level = 2, Label = "P. Jawab Afd", JabatanKode = "ASISTEN_WILAYAH", MenuCode = "QC_APPROVAL", ScopeByAfdeling = true, Active = true },
                new QcFormApprovalStep { FormId = 1, Level = 3, Label = "Diperiksa", JabatanKode = "ASST_QC", MenuCode = "QC_APPROVAL", Active = true },
                new QcFormApprovalStep { FormId = 1, Level = 4, Label = "Disetujui", JabatanKode = "SNR_MGR_QC", MenuCode = "QC_APPROVAL", Active = true });
        }
        db.QcInspections.Add(new QcInspection { Id = 1, FormId = 1, Form = form, CreatedBy = "reporter", Commodity = form.Commodity,
            Status = QcInspectionStatus.Draft, InspectionDate = new DateOnly(2026, 11, 13) });
        await db.SaveChangesAsync();
        return db;
    }
}