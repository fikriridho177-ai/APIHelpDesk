using AuthUtility.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(
        DbContextOptions<AppDbContext> options
    ) : base(options)
    {
    }

    public DbSet<UtlUser> Users =>
        Set<UtlUser>();

    public DbSet<UtlGroup> Groups =>
        Set<UtlGroup>();

    public DbSet<UtlMenu> Menus =>
        Set<UtlMenu>();

    public DbSet<UtlGroupMenu> GroupMenus =>
        Set<UtlGroupMenu>();

    public DbSet<UtlPasswordHistory> PasswordHistories =>
        Set<UtlPasswordHistory>();

    public DbSet<MstCompany> Companies =>
        Set<MstCompany>();

    public DbSet<MstDepartment> Departments =>
        Set<MstDepartment>();

    public DbSet<MstItemSubCategory> ItemSubCategories =>
        Set<MstItemSubCategory>();

    public DbSet<UtlGroupCompany> GroupCompanies =>
        Set<UtlGroupCompany>();

    public DbSet<UtlGroupDepartment> GroupDepartments =>
        Set<UtlGroupDepartment>();

    public DbSet<UtlGroupItemSubCategory> GroupItemSubCategories =>
        Set<UtlGroupItemSubCategory>();

    public DbSet<ChatGroup> ChatGroups =>
        Set<ChatGroup>();

    public DbSet<ChatRole> ChatRoles =>
        Set<ChatRole>();

    public DbSet<ChatMember> ChatMembers =>
        Set<ChatMember>();

    public DbSet<ChatMessage> ChatMessages =>
        Set<ChatMessage>();

    public DbSet<LynRequest> LynRequests =>
        Set<LynRequest>();

    public DbSet<LynRequestLog> LynRequestLogs =>
        Set<LynRequestLog>();

    public DbSet<LynJenisPermintaan> LynJenisPermintaan =>
        Set<LynJenisPermintaan>();

    public DbSet<SnsKebun> SnsKebuns => Set<SnsKebun>();
    public DbSet<SnsAfdeling> SnsAfdelings => Set<SnsAfdeling>();
    public DbSet<SnsBlok> SnsBloks => Set<SnsBlok>();
    public DbSet<SnsJenis> SnsJenis => Set<SnsJenis>();
    public DbSet<SnsParameter> SnsParameters => Set<SnsParameter>();
    public DbSet<SnsHeader> SnsHeaders => Set<SnsHeader>();
    public DbSet<SnsDetail> SnsDetails => Set<SnsDetail>();
    public DbSet<SnsFoto> SnsFotos => Set<SnsFoto>();
    public DbSet<SnsLog> SnsLogs => Set<SnsLog>();

    public DbSet<IotDeviceAgent> IotDeviceAgents => Set<IotDeviceAgent>();
    public DbSet<IotDeviceCredential> IotDeviceCredentials => Set<IotDeviceCredential>();
    public DbSet<IotDeviceReport> IotDeviceReports => Set<IotDeviceReport>();
    public DbSet<IotSensorType> IotSensorTypes => Set<IotSensorType>();
    public DbSet<IotDeviceSensor> IotDeviceSensors => Set<IotDeviceSensor>();
    public DbSet<IotDeviceConfiguration> IotDeviceConfigurations => Set<IotDeviceConfiguration>();
    public DbSet<IotFirmwarePackage> IotFirmwarePackages => Set<IotFirmwarePackage>();
    public DbSet<IotOtaJob> IotOtaJobs => Set<IotOtaJob>();
    public DbSet<IotOtaEvent> IotOtaEvents => Set<IotOtaEvent>();
    public DbSet<IotActivityEvent> IotActivityEvents => Set<IotActivityEvent>();

    // QC module (PostgreSQL HelpDesk — transaksi QC). Lihat 2026-11-11-qc-core-schema.sql
    public DbSet<QcForm> QcForms => Set<QcForm>();
    public DbSet<QcInspection> QcInspections => Set<QcInspection>();
    public DbSet<QcApproval> QcApprovals => Set<QcApproval>();
    public DbSet<QcFinding> QcFindings => Set<QcFinding>();
    public DbSet<QcHistory> QcHistories => Set<QcHistory>();


    protected override void OnModelCreating(
        ModelBuilder modelBuilder
    )
    {
        base.OnModelCreating(modelBuilder);


        /* =====================================================
           TABLE MAPPING
        ===================================================== */

        modelBuilder.Entity<UtlUser>()
            .ToTable("UtlUser");

        modelBuilder.Entity<UtlUser>()
            .Property(x => x.Nik)
            .HasColumnName("NIK");

        modelBuilder.Entity<UtlUser>()
            .Property(x => x.RegNo)
            .HasColumnName("RegNo");

        modelBuilder.Entity<UtlUser>()
            .Property(x => x.DeptId)
            .HasColumnName("DeptID");

        modelBuilder.Entity<UtlUser>()
            .Property(x => x.CompanyId)
            .HasColumnName("CompanyID");

        modelBuilder.Entity<UtlGroup>()
            .ToTable("UtlGroup");

        modelBuilder.Entity<UtlMenu>()
            .ToTable("UtlMenu");

        modelBuilder.Entity<UtlGroupMenu>()
            .ToTable("UtlGroupMenu");

        modelBuilder.Entity<UtlPasswordHistory>()
            .ToTable("UtlPasswordHistory");

        modelBuilder.Entity<MstCompany>()
            .ToTable("MstCompany");

        modelBuilder.Entity<MstDepartment>()
            .ToTable("MstDepartment");

        // Master Company/Department lokal tidak lagi menjadi sumber relasi scope.
        // Ignore navigation collections agar EF Core tidak membuat shadow FK
        // MstCompanyId / MstDepartmentId pada UtlGroupCompany/UtlGroupDepartment.
        modelBuilder.Entity<MstCompany>()
            .Ignore(x => x.GroupCompanies);

        modelBuilder.Entity<MstDepartment>()
            .Ignore(x => x.GroupDepartments);

        modelBuilder.Entity<MstItemSubCategory>()
            .ToTable("MstItemSubCategory");

        modelBuilder.Entity<UtlGroupCompany>()
            .ToTable("UtlGroupCompany");

        modelBuilder.Entity<UtlGroupDepartment>()
            .ToTable("UtlGroupDepartment");

        modelBuilder.Entity<UtlGroupItemSubCategory>()
            .ToTable("UtlGroupItemSubCategory");


        /* =====================================================
           USER -> GROUP
        ===================================================== */

        modelBuilder.Entity<UtlGroup>()
            .HasMany(x => x.Users)
            .WithOne(x => x.Group)
            .HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Restrict);


        /* =====================================================
           MENU PARENT / CHILD
        ===================================================== */

        modelBuilder.Entity<UtlMenu>()
            .HasOne(x => x.Parent)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.SetNull);


        /* =====================================================
           GROUP MENU
        ===================================================== */

        modelBuilder.Entity<UtlGroupMenu>()
            .HasIndex(x => new
            {
                x.GroupId,
                x.MenuId
            })
            .IsUnique();

        modelBuilder.Entity<UtlGroupMenu>()
            .HasOne(x => x.Group)
            .WithMany(x => x.GroupMenus)
            .HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UtlGroupMenu>()
            .HasOne(x => x.Menu)
            .WithMany(x => x.GroupMenus)
            .HasForeignKey(x => x.MenuId)
            .OnDelete(DeleteBehavior.Cascade);


        /* =====================================================
           PASSWORD HISTORY
        ===================================================== */

        modelBuilder.Entity<UtlPasswordHistory>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);


        /* =====================================================
           GROUP COMPANY
           DATABASE PRIMARY KEY:
           GroupId + CompanyId
        ===================================================== */

        modelBuilder.Entity<UtlGroupCompany>()
            .HasKey(x => new
            {
                x.GroupId,
                x.CompanyId
            });

        modelBuilder.Entity<UtlGroupCompany>()
            .HasOne(x => x.Group)
            .WithMany(x => x.GroupCompanies)
            .HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        /* =====================================================
           GROUP DEPARTMENT
           DATABASE PRIMARY KEY:
           GroupId + DepartmentId
        ===================================================== */

        modelBuilder.Entity<UtlGroupDepartment>()
            .HasKey(x => new
            {
                x.GroupId,
                x.DepartmentId
            });

        modelBuilder.Entity<UtlGroupDepartment>()
            .HasOne(x => x.Group)
            .WithMany(x => x.GroupDepartments)
            .HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        /* =====================================================
           GROUP ITEM SUB CATEGORY

           Yang ini database Anda masih punya Id,
           jadi tidak diubah menjadi composite key.
        ===================================================== */

        modelBuilder.Entity<UtlGroupItemSubCategory>()
            .HasIndex(x => new
            {
                x.GroupId,
                x.ItemSubCategoryId
            })
            .IsUnique();

        modelBuilder.Entity<UtlGroupItemSubCategory>()
            .HasOne(x => x.Group)
            .WithMany(x => x.GroupItemSubCategories)
            .HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UtlGroupItemSubCategory>()
            .HasOne(x => x.ItemSubCategory)
            .WithMany(x => x.GroupItemSubCategories)
            .HasForeignKey(x => x.ItemSubCategoryId)
            .OnDelete(DeleteBehavior.Cascade);


        /* =====================================================
           LIVE CHAT
           Lihat POSTGRES_LIVE_CHAT_MIGRATION.sql
        ===================================================== */

        modelBuilder.Entity<ChatGroup>(entity =>
        {
            entity.ToTable("ChatGroup");

            entity.HasMany(x => x.Roles)
                .WithOne()
                .HasForeignKey(x => x.GroupId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.Members)
                .WithOne()
                .HasForeignKey(x => x.GroupId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChatRole>(entity =>
        {
            entity.ToTable("ChatRole");
            entity.Property(x => x.Permissions).HasConversion<long>();
        });

        modelBuilder.Entity<ChatMember>(entity =>
        {
            entity.ToTable("ChatMember");
            entity.HasKey(x => new { x.GroupId, x.UserId });

            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId);

            entity.HasOne(x => x.Role)
                .WithMany()
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.ToTable("ChatMessage");

            entity.HasOne(x => x.Sender)
                .WithMany()
                .HasForeignKey(x => x.SenderUserId);
        });


        /* =====================================================
           LAYANAN (pengajuan)
           Lihat POSTGRES_LAYANAN_MIGRATION.sql
        ===================================================== */

        modelBuilder.Entity<LynRequest>(entity =>
        {
            entity.ToTable("LynRequest");

            entity.HasOne(x => x.Menu)
                .WithMany()
                .HasForeignKey(x => x.MenuId);

            entity.HasOne(x => x.Requester)
                .WithMany()
                .HasForeignKey(x => x.RequesterUserId);

            entity.HasOne(x => x.Jenis)
                .WithMany()
                .HasForeignKey(x => x.JenisId);

            entity.HasMany(x => x.Logs)
                .WithOne()
                .HasForeignKey(x => x.RequestId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LynJenisPermintaan>(entity =>
        {
            entity.ToTable("LynJenisPermintaan");

            entity.HasOne(x => x.DivisionMenu)
                .WithMany()
                .HasForeignKey(x => x.DivisionMenuId);
        });

        modelBuilder.Entity<LynRequestLog>(entity =>
        {
            entity.ToTable("LynRequestLog");

            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId);
        });


        /* =====================================================
           SENSUS
           Lihat POSTGRES_SENSUS_MIGRATION.sql
        ===================================================== */

        modelBuilder.Entity<SnsKebun>().ToTable("SnsKebun");

        modelBuilder.Entity<SnsAfdeling>(entity =>
        {
            entity.ToTable("SnsAfdeling");
            entity.HasOne(x => x.Kebun).WithMany().HasForeignKey(x => x.KebunId);
        });

        modelBuilder.Entity<SnsBlok>(entity =>
        {
            entity.ToTable("SnsBlok");
            entity.HasOne(x => x.Afdeling).WithMany().HasForeignKey(x => x.AfdelingId);
        });

        modelBuilder.Entity<SnsJenis>(entity =>
        {
            entity.ToTable("MstHpt");
            entity.HasMany(x => x.Parameters)
                .WithOne()
                .HasForeignKey(x => x.JenisId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SnsParameter>().ToTable("MstParameterHpt");

        modelBuilder.Entity<SnsHeader>(entity =>
        {
            entity.ToTable("SnsHeader");
            entity.HasOne(x => x.Jenis).WithMany().HasForeignKey(x => x.JenisId);
            entity.HasOne(x => x.Blok).WithMany().HasForeignKey(x => x.BlokId);
            entity.HasOne(x => x.Petugas).WithMany().HasForeignKey(x => x.PetugasUserId);
            entity.HasOne(x => x.ApprovedBy).WithMany().HasForeignKey(x => x.ApprovedByUserId);

            entity.HasMany(x => x.Details)
                .WithOne()
                .HasForeignKey(x => x.HeaderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.Fotos)
                .WithOne()
                .HasForeignKey(x => x.HeaderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SnsDetail>(entity =>
        {
            entity.ToTable("SnsDetail");
            entity.HasOne(x => x.Parameter).WithMany().HasForeignKey(x => x.ParameterId);
        });

        modelBuilder.Entity<SnsFoto>().ToTable("SnsFoto");

        modelBuilder.Entity<SnsLog>(entity =>
        {
            entity.ToTable("SnsLog");
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<IotDeviceAgent>(entity =>
        {
            entity.ToTable("IotDeviceAgent");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.DeviceId).IsUnique();
            entity.Property(x => x.DeviceId).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.DeviceType).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Platform).HasMaxLength(64).IsRequired();
            entity.Property(x => x.BoardType).HasMaxLength(128).IsRequired();
            entity.Property(x => x.BoardRevision).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Location).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.CapabilitiesJson).HasColumnType("jsonb");
        });

        modelBuilder.Entity<IotDeviceCredential>(entity =>
        {
            entity.ToTable("IotDeviceCredential");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.KeyPrefix).IsUnique();
            entity.Property(x => x.KeyPrefix).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Salt).HasColumnType("bytea").IsRequired();
            entity.Property(x => x.KeyHash).HasColumnType("bytea").IsRequired();
            entity.HasOne(x => x.DeviceAgent)
                .WithMany(x => x.Credentials)
                .HasForeignKey(x => x.DeviceAgentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IotDeviceReport>(entity =>
        {
            entity.ToTable("IotDeviceReport");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.DeviceAgentId, x.DeviceTimestamp });
            entity.HasIndex(x => new { x.DeviceAgentId, x.DeviceTimestamp, x.PayloadHash }).IsUnique();
            entity.Property(x => x.Kind).HasMaxLength(32).IsRequired();
            entity.Property(x => x.FirmwareVersion).HasMaxLength(128).IsRequired();
            entity.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.PayloadHash).HasMaxLength(128).IsRequired();
            entity.HasOne(x => x.DeviceAgent)
                .WithMany(x => x.Reports)
                .HasForeignKey(x => x.DeviceAgentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IotSensorType>(entity =>
        {
            entity.ToTable("IotSensorType");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Model).HasMaxLength(128);
            entity.Property(x => x.Protocol).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Unit).HasMaxLength(64).IsRequired();
            entity.Property(x => x.ParametersJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.PinRequirementsJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.DriverKey).HasMaxLength(128).IsRequired();
        });

        modelBuilder.Entity<IotDeviceSensor>(entity =>
        {
            entity.ToTable("IotDeviceSensor");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.DeviceAgentId, x.Label }).IsUnique();
            entity.Property(x => x.Label).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Protocol).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Unit).HasMaxLength(64).IsRequired();
            entity.Property(x => x.PinMappingsJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.CalibrationJson).HasColumnType("jsonb");
            entity.HasOne(x => x.DeviceAgent)
                .WithMany(x => x.Sensors)
                .HasForeignKey(x => x.DeviceAgentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.SensorType)
                .WithMany(x => x.DeviceSensors)
                .HasForeignKey(x => x.SensorTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<IotDeviceConfiguration>(entity =>
        {
            entity.ToTable("IotDeviceConfiguration");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.DeviceAgentId).IsUnique();
            entity.Property(x => x.DesiredJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.DesiredStatus).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.ReportedJson).HasColumnType("jsonb");
            entity.Property(x => x.ReportedStatus).HasConversion<string>().HasMaxLength(32);
            entity.HasOne(x => x.DeviceAgent)
                .WithOne(x => x.Configuration)
                .HasForeignKey<IotDeviceConfiguration>(x => x.DeviceAgentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IotFirmwarePackage>(entity =>
        {
            entity.ToTable("IotFirmwarePackage");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TargetMcu, x.Version }).IsUnique();
            entity.Property(x => x.PackageName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Version).HasMaxLength(128).IsRequired();
            entity.Property(x => x.TargetMcu).HasMaxLength(64).IsRequired();
            entity.Property(x => x.CompatibleBoardProfilesJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.CompatibleHardwareRevisionsJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.FileName).HasMaxLength(255).IsRequired();
            entity.Property(x => x.StoragePath).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Sha256).HasMaxLength(128).IsRequired();
            entity.Property(x => x.SignatureStatus).HasMaxLength(32).IsRequired();
        });

        modelBuilder.Entity<IotOtaJob>(entity =>
        {
            entity.ToTable("IotOtaJob");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.DeviceAgentId, x.Status });
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.FailureReason).HasMaxLength(2000);
            entity.Property(x => x.CurrentFirmwareVersion).HasMaxLength(128);
            entity.Property(x => x.ReportedFirmwareVersion).HasMaxLength(128);
            entity.HasOne(x => x.DeviceAgent)
                .WithMany(x => x.OtaJobs)
                .HasForeignKey(x => x.DeviceAgentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.FirmwarePackage)
                .WithMany(x => x.OtaJobs)
                .HasForeignKey(x => x.FirmwarePackageId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<IotOtaEvent>(entity =>
        {
            entity.ToTable("IotOtaEvent");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OtaJobId, x.Sequence }).IsUnique();
            entity.Property(x => x.EventType).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Message).HasMaxLength(2000);
            entity.Property(x => x.ReportedFirmwareVersion).HasMaxLength(128);
            entity.HasOne(x => x.OtaJob)
                .WithMany(x => x.Events)
                .HasForeignKey(x => x.OtaJobId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IotActivityEvent>(entity =>
        {
            entity.ToTable("IotActivityEvent");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.DeviceAgentId, x.OccurredAt });
            entity.Property(x => x.EventType).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.DetailsJson).HasColumnType("jsonb");
            entity.HasOne(x => x.DeviceAgent)
                .WithMany(x => x.ActivityEvents)
                .HasForeignKey(x => x.DeviceAgentId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        /* =====================================================
           QC MODULE
           Lihat sql/2026-11-11-qc-core-schema.sql
           Konvensi: timestamptz eksplisit, jsonb, FK dinamai, index IX_...
           ===================================================== */

        // QcForm — master jenis pemeriksaan
        modelBuilder.Entity<QcForm>(entity =>
        {
            entity.ToTable("QcForm");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Commodity).HasMaxLength(32).IsRequired();
            entity.Property(x => x.CreatedBy).HasMaxLength(100);
            entity.Property(x => x.UpdatedBy).HasMaxLength(100);
            entity.Property(x => x.CreatedAt).HasColumnType("timestamptz");
        });

        // QcInspection — satu tabel transaksi utama
        modelBuilder.Entity<QcInspection>(entity =>
        {
            entity.ToTable("QcInspection");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.InspectionNo).IsUnique();
            entity.HasIndex(x => x.FormId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.CompanyId);
            entity.HasIndex(x => x.WilayahKode);
            entity.HasIndex(x => x.Commodity);
            entity.HasIndex(x => x.InspectionDate);
            entity.HasIndex(x => new { x.PeriodYear, x.PeriodMonth });
            entity.HasIndex(x => new { x.Status, x.CurrentApprovalLevel });

            entity.Property(x => x.InspectionNo).HasMaxLength(50);
            entity.Property(x => x.CompanyNameSnapshot).HasMaxLength(200);
            entity.Property(x => x.WilayahKode).HasMaxLength(50);
            entity.Property(x => x.PersilSumber).HasMaxLength(20);
            entity.Property(x => x.PersilId).HasMaxLength(100);
            entity.Property(x => x.PersilKode).HasMaxLength(100);
            entity.Property(x => x.KodeAfdeling).HasMaxLength(50);
            entity.Property(x => x.Commodity).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
            entity.Property(x => x.CreatedBy).HasMaxLength(100);
            entity.Property(x => x.UpdatedBy).HasMaxLength(100);
            entity.Property(x => x.DataJson).HasColumnType("jsonb");
            entity.Property(x => x.CreatedAt).HasColumnType("timestamptz");
            entity.Property(x => x.UpdatedAt).HasColumnType("timestamptz");
            entity.Property(x => x.SubmittedAt).HasColumnType("timestamptz");
            entity.Property(x => x.ApprovedAt).HasColumnType("timestamptz");
            entity.Property(x => x.ClosedAt).HasColumnType("timestamptz");

            entity.HasOne(x => x.Form)
                .WithMany(x => x.Inspections)
                .HasForeignKey(x => x.FormId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Approvals)
                .WithOne(x => x.Inspection)
                .HasForeignKey(x => x.InspectionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.Findings)
                .WithOne(x => x.Inspection)
                .HasForeignKey(x => x.InspectionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Histories)
                .WithOne(x => x.Inspection)
                .HasForeignKey(x => x.InspectionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // QcApproval — satu row per (InspectionId, Level)
        modelBuilder.Entity<QcApproval>(entity =>
        {
            entity.ToTable("QcApproval");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.InspectionId, x.Level }).IsUnique();
            entity.HasIndex(x => x.Status);

            entity.Property(x => x.Action).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
            entity.Property(x => x.ApproverNameSnapshot).HasMaxLength(150);
            entity.Property(x => x.CreatedAt).HasColumnType("timestamptz");
            entity.Property(x => x.UpdatedAt).HasColumnType("timestamptz");
            entity.Property(x => x.ApprovedAt).HasColumnType("timestamptz");

            entity.HasOne(x => x.Inspection)
                .WithMany(x => x.Approvals)
                .HasForeignKey(x => x.InspectionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Approver)
                .WithMany()
                .HasForeignKey(x => x.ApproverUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.ApproverGroup)
                .WithMany()
                .HasForeignKey(x => x.ApproverGroupId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // QcFinding — temuan
        modelBuilder.Entity<QcFinding>(entity =>
        {
            entity.ToTable("QcFinding");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.FindingNo).IsUnique();
            entity.HasIndex(x => x.InspectionId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.TargetDate);
            entity.HasIndex(x => x.Severity);

            entity.Property(x => x.FindingNo).HasMaxLength(50);
            entity.Property(x => x.Category).HasMaxLength(100);
            entity.Property(x => x.Description).IsRequired();
            entity.Property(x => x.Severity).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
            entity.Property(x => x.PicNameSnapshot).HasMaxLength(150);
            entity.Property(x => x.FollowUpNote);
            entity.Property(x => x.CreatedBy).HasMaxLength(100);
            entity.Property(x => x.UpdatedBy).HasMaxLength(100);
            entity.Property(x => x.CreatedAt).HasColumnType("timestamptz");
            entity.Property(x => x.UpdatedAt).HasColumnType("timestamptz");

            entity.HasOne(x => x.Inspection)
                .WithMany(x => x.Findings)
                .HasForeignKey(x => x.InspectionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.PicUser)
                .WithMany()
                .HasForeignKey(x => x.PicUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.VerifiedBy)
                .WithMany()
                .HasForeignKey(x => x.VerifiedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // QcHistory — audit trail (immutable)
        modelBuilder.Entity<QcHistory>(entity =>
        {
            entity.ToTable("QcHistory");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.InspectionId, x.CreatedAt });

            entity.Property(x => x.Action).HasMaxLength(32).IsRequired();
            entity.Property(x => x.FromStatus).HasMaxLength(32);
            entity.Property(x => x.ToStatus).HasMaxLength(32);
            entity.Property(x => x.CreatedAt).HasColumnType("timestamptz");

            entity.HasOne(x => x.Inspection)
                .WithMany(x => x.Histories)
                .HasForeignKey(x => x.InspectionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
