using System.Security.Claims;
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

// Fase 2: lifecycle temuan OPEN -> IN_PROGRESS -> DONE -> VERIFIED (terminal),
// dan CLOSE inspeksi hanya bila APPROVED + semua temuan VERIFIED.
public class QcFindingFlowTests
{
    private static IConfiguration Settings() => new ConfigurationBuilder()
        .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: false).Build();

    [Fact]
    public async Task FullFindingLifecycleWithStampedNotesAndVerifier()
    {
        await using var db = await Database();
        var controller = Controller(db);

        var created = Assert.IsType<OkObjectResult>(await controller.CreateFinding(new CreateQcFindingRequest
        {
            InspectionId = 1, Description = "Jalur tidak dibersihkan", Severity = "MAJOR",
            Category = "KEBERSIHAN", TargetDate = new DateOnly(2026, 11, 25)
        }, default));
        var detail = Assert.IsType<QcFindingDetailDto>(created.Value);
        Assert.Equal(QcFindingStatus.Open, detail.Status);
        Assert.StartsWith("QC-KLP-F-", detail.FindingNo);

        // Verify belum boleh saat OPEN.
        Assert.IsType<BadRequestObjectResult>(await controller.Verify(detail.Id, default));

        var followed = Assert.IsType<OkObjectResult>(await controller.FollowUp(detail.Id,
            new QcFindingFollowUpRequest { FollowUpNote = "Sudah dijadwalkan ulang" }, default));
        var inProgress = Assert.IsType<QcFindingDetailDto>(followed.Value);
        Assert.Equal(QcFindingStatus.InProgress, inProgress.Status);
        Assert.Contains("Test Reporter] Sudah dijadwalkan ulang", inProgress.FollowUpNote);

        // Catatan menumpuk, bukan menimpa.
        await controller.FollowUp(detail.Id, new QcFindingFollowUpRequest { FollowUpNote = "Progres 50%" }, default);

        var done = Assert.IsType<OkObjectResult>(await controller.Done(detail.Id,
            new QcFindingFollowUpRequest { FollowUpNote = "Selesai seluruhnya" }, default));
        Assert.Equal(QcFindingStatus.Done, Assert.IsType<QcFindingDetailDto>(done.Value).Status);

        var verified = Assert.IsType<OkObjectResult>(await controller.Verify(detail.Id, default));
        var final = Assert.IsType<QcFindingDetailDto>(verified.Value);
        Assert.Equal(QcFindingStatus.Verified, final.Status);
        Assert.Equal(1, final.VerifiedByUserId);
        Assert.NotNull(final.VerifiedDate);
        Assert.False(final.Overdue);
        Assert.Equal(3, final.FollowUpNote!.Split('\n').Length);

        // Terminal: tidak ada aksi lanjutan.
        Assert.IsType<BadRequestObjectResult>(await controller.FollowUp(detail.Id, new QcFindingFollowUpRequest { FollowUpNote = "X" }, default));
        Assert.IsType<BadRequestObjectResult>(await controller.Done(detail.Id, null, default));
        Assert.IsType<BadRequestObjectResult>(await controller.Verify(detail.Id, default));
    }

    [Fact]
    public async Task InspectionCloseRequiresApprovedAndAllFindingsVerified()
    {
        await using var db = await Database();
        var findings = FindingController(db);
        var inspections = InspectionController(db);

        var created = Assert.IsType<OkObjectResult>(await findings.CreateFinding(new CreateQcFindingRequest
        {
            InspectionId = 1, Description = "Temuan satu", Severity = "MINOR"
        }, default));
        var id = Assert.IsType<QcFindingDetailDto>(created.Value).Id;

        // APPROVED tapi masih ada temuan aktif -> tidak bisa ditutup.
        Assert.IsType<BadRequestObjectResult>(await inspections.Close(1, null, default));

        await findings.FollowUp(id, new QcFindingFollowUpRequest { FollowUpNote = "Dikerjakan" }, default);
        await findings.Done(id, null, default);
        // DONE masih aktif (belum diverifikasi) -> tetap tidak bisa ditutup.
        Assert.IsType<BadRequestObjectResult>(await inspections.Close(1, null, default));

        await findings.Verify(id, default);
        Assert.IsType<OkObjectResult>(await inspections.Close(1, null, default));
        Assert.Equal(QcInspectionStatus.Closed, db.QcInspections.Single().Status);
        Assert.NotNull(db.QcInspections.Single().ClosedAt);

        // CLOSED = terminal: temuan baru tidak bisa dibuat.
        Assert.IsType<BadRequestObjectResult>(await findings.CreateFinding(new CreateQcFindingRequest
        {
            InspectionId = 1, Description = "Terlambat", Severity = "MINOR"
        }, default));
    }

    [Fact]
    public async Task DoneAllowedDirectlyFromOpenAndSeverityValidated()
    {
        await using var db = await Database();
        var controller = Controller(db);
        Assert.IsType<BadRequestObjectResult>(await controller.CreateFinding(new CreateQcFindingRequest
        {
            InspectionId = 1, Description = "X", Severity = "PARAH"
        }, default));

        var created = Assert.IsType<OkObjectResult>(await controller.CreateFinding(new CreateQcFindingRequest
        {
            InspectionId = 1, Description = "Langsung selesai", Severity = "MINOR"
        }, default));
        var id = Assert.IsType<QcFindingDetailDto>(created.Value).Id;
        var done = Assert.IsType<OkObjectResult>(await controller.Done(id, null, default));
        Assert.Equal(QcFindingStatus.Done, Assert.IsType<QcFindingDetailDto>(done.Value).Status);
    }

    private static ControllerContext Context() => new() { HttpContext = new DefaultHttpContext
    {
        User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "1") }, "test"))
    } };

    private static QcFindingsController Controller(AppDbContext db) => FindingController(db);

    private static QcFindingsController FindingController(AppDbContext db) =>
        new(db, new QcNumberGenerator(Settings(), new InMemoryQcNumberSeqStore()), new QcStatusEngine())
        { ControllerContext = Context() };

    private static QcInspectionsController InspectionController(AppDbContext db) =>
        new(db, new QcStatusEngine(), new QcNumberGenerator(Settings(), new InMemoryQcNumberSeqStore()), new QcApprovalFlow(db))
        { ControllerContext = Context() };

    private static async Task<AppDbContext> Database()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options;
        var db = new AppDbContext(options);
        var group = new UtlGroup { Id = 1, Code = "QC_TEAM", Name = "Test QC", Active = true };
        db.Groups.Add(group);
        db.Menus.AddRange(
            new UtlMenu { Id = 1, Code = "QC_FINDING", Name = "QC Finding", Active = true },
            new UtlMenu { Id = 2, Code = "QC_APPROVAL", Name = "QC Approval", Active = true });
        db.Users.Add(new UtlUser { Id = 1, UserId = "reporter", Name = "Test Reporter", PasswordHash = "test", GroupId = 1, Group = group, Active = true });
        db.GroupMenus.AddRange(
            new UtlGroupMenu { GroupId = 1, MenuId = 1, CanCreate = true, CanUpdate = true, CanRead = true, CanView = true },
            new UtlGroupMenu { GroupId = 1, MenuId = 2, CanApprove = true });
        var form = new QcForm { Id = 1, Code = "QC-PANEN-KLP", Name = "Panen Kelapa", Commodity = "KELAPA" };
        db.QcForms.Add(form);
        db.QcInspections.Add(new QcInspection { Id = 1, FormId = 1, Form = form, CreatedBy = "reporter", Commodity = "KELAPA",
            Status = QcInspectionStatus.Approved, InspectionDate = new DateOnly(2026, 11, 13), ApprovedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        return db;
    }
}