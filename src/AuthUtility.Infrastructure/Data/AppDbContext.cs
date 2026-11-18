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
    public DbSet<SnsHptRule> SnsHptRules => Set<SnsHptRule>();
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

    // Laporan lapangan WAD (PostgreSQL HelpDesk). Lihat 2026-11-16-wad-reporting.sql
    public DbSet<WadObject> WadObjects => Set<WadObject>();
    public DbSet<WadReport> WadReports => Set<WadReport>();
    public DbSet<WadReportPhoto> WadReportPhotos => Set<WadReportPhoto>();

    // Data operasional riil WAD (impor paket Helpdesk ASD).
    public DbSet<WadBuilding> WadBuildings => Set<WadBuilding>();
    public DbSet<WadOverflowLog> WadOverflowLogs => Set<WadOverflowLog>();
    public DbSet<WadOverflowLogPhoto> WadOverflowLogPhotos => Set<WadOverflowLogPhoto>();
    public DbSet<WadSubsidenObservation> WadSubsidenObservations => Set<WadSubsidenObservation>();
    public DbSet<WadSubsidenPhoto> WadSubsidenPhotos => Set<WadSubsidenPhoto>();
    public DbSet<WadSiponInspection> WadSiponInspections => Set<WadSiponInspection>();
    public DbSet<WadFloodedLandObservation> WadFloodedLandObservations => Set<WadFloodedLandObservation>();


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
            entity.ToTable("UtlChatGroup");

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
            entity.ToTable("UtlChatRole");
            entity.Property(x => x.Permissions).HasConversion<long>();
        });

        modelBuilder.Entity<ChatMember>(entity =>
        {
            entity.ToTable("UtlChatMember");
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
            entity.ToTable("TrnChatMessage");

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
            entity.ToTable("TrnLynRequest");

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
            entity.ToTable("MstLynJenisPermintaan");

            entity.HasOne(x => x.DivisionMenu)
                .WithMany()
                .HasForeignKey(x => x.DivisionMenuId);
        });

        modelBuilder.Entity<LynRequestLog>(entity =>
        {
            entity.ToTable("HistoryLynRequestLog");

            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId);
        });


        /* =====================================================
           SENSUS
           Lihat POSTGRES_SENSUS_MIGRATION.sql
        ===================================================== */

        modelBuilder.Entity<SnsKebun>().ToTable("MstSnsKebun");

        modelBuilder.Entity<SnsAfdeling>(entity =>
        {
            entity.ToTable("MstSnsAfdeling");
            entity.HasOne(x => x.Kebun).WithMany().HasForeignKey(x => x.KebunId);
        });

        modelBuilder.Entity<SnsBlok>(entity =>
        {
            entity.ToTable("MstSnsBlok");
            entity.HasOne(x => x.Afdeling).WithMany().HasForeignKey(x => x.AfdelingId);
        });

        modelBuilder.Entity<SnsJenis>(entity =>
        {
            entity.ToTable("MstHpt");
            entity.HasMany(x => x.Rules).WithOne().HasForeignKey(x => x.JenisId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Parameters)
                .WithOne()
                .HasForeignKey(x => x.JenisId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SnsParameter>().ToTable("MstHptParameter");

        modelBuilder.Entity<SnsHptRule>(entity =>
        {
            entity.ToTable("MstHptRule");
            entity.HasIndex(x => new { x.JenisId, x.Active });
            entity.HasIndex(x => x.SeedKey).IsUnique();
            entity.Property(x => x.ThresholdValue).HasPrecision(19, 6);
        });

        modelBuilder.Entity<SnsHeader>(entity =>
        {
            entity.ToTable("TrnSnsHeader");
            entity.Property(x => x.LuasHa).HasPrecision(18, 4);
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
            entity.ToTable("TrnSnsDetail");
            entity.HasOne(x => x.Parameter).WithMany().HasForeignKey(x => x.ParameterId);
        });

        modelBuilder.Entity<SnsFoto>().ToTable("TrnSnsFoto");

        modelBuilder.Entity<SnsLog>(entity =>
        {
            entity.ToTable("HistorySnsLog");
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
            entity.HasIndex(x => x.JobNumber).IsUnique();
            entity.Property(x => x.JobNumber).ValueGeneratedOnAdd();
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
            entity.ToTable("MstQcForm");
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
            entity.ToTable("TrnQcInspection");
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
            entity.ToTable("TrnQcApproval");
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
            entity.ToTable("TrnQcFinding");
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
            entity.ToTable("HistoryQcInspection");
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

        // WadObject — master bangunan/patok/persil/projek WAD
        modelBuilder.Entity<WadObject>(entity =>
        {
            entity.ToTable("WadObject");
            entity.Property(x => x.MeasurementMethod).HasMaxLength(30);
            entity.Property(x => x.VerifyDropCm).HasPrecision(14, 3);
            entity.Property(x => x.CriticalDropCm).HasPrecision(14, 3);
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.Module, x.Code }).IsUnique();
            entity.HasIndex(x => x.Module);

            entity.Property(x => x.Module).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ObjectType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Wilayah).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Location).HasMaxLength(200);
            entity.Property(x => x.ReferenceValueCm).HasPrecision(14, 3);
            entity.Property(x => x.AreaHa).HasPrecision(12, 4);
            entity.Property(x => x.PicName).HasMaxLength(150);
            entity.Property(x => x.TargetQty).HasPrecision(14, 3);
            entity.Property(x => x.Unit).HasMaxLength(30);
            entity.Property(x => x.CreatedAt).HasColumnType("timestamptz");
        });

        // WadReport — satu laporan lapangan untuk satu objek
        modelBuilder.Entity<WadReport>(entity =>
        {
            entity.ToTable("WadReport");
            entity.HasIndex(x => new { x.CreatedByUserId, x.RequestId }).IsUnique();
            entity.Property(x => x.MeasurementMethod).HasMaxLength(30);
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.Module, x.ObservedDate });
            entity.HasIndex(x => x.ObjectId);

            entity.Property(x => x.Module).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Condition).HasMaxLength(30);
            entity.Property(x => x.WaterLevelCm).HasPrecision(10, 2);
            entity.Property(x => x.MeasurementValue).HasPrecision(14, 3);
            entity.Property(x => x.MeasurementUnit).HasMaxLength(10);
            entity.Property(x => x.MeasuredValueCm).HasPrecision(14, 3);
            entity.Property(x => x.ReferenceValueCm).HasPrecision(14, 3);
            entity.Property(x => x.DropCm).HasPrecision(14, 3);
            entity.Property(x => x.FloodAreaHa).HasPrecision(12, 4);
            entity.Property(x => x.FloodHeightCm).HasPrecision(10, 2);
            entity.Property(x => x.WorkDescription).HasMaxLength(2000);
            entity.Property(x => x.RealizationQty).HasPrecision(14, 3);
            entity.Property(x => x.RealizationUnit).HasMaxLength(30);
            entity.Property(x => x.Constraints).HasMaxLength(2000);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.CreatedByName).HasMaxLength(150).IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnType("timestamptz");
            entity.Property(x => x.UpdatedAt).HasColumnType("timestamptz");

            entity.HasOne(x => x.Object)
                .WithMany()
                .HasForeignKey(x => x.ObjectId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Photos)
                .WithOne(x => x.Report)
                .HasForeignKey(x => x.ReportId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WadReportPhoto>(entity =>
        {
            entity.ToTable("WadReportPhoto");
            entity.HasIndex(x => new { x.ReportId, x.RequestId }).IsUnique();
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.ReportId);

            entity.Property(x => x.FileUrl).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Keterangan).HasMaxLength(200);
            entity.Property(x => x.Phase).HasMaxLength(20);
            entity.Property(x => x.CreatedAt).HasColumnType("timestamptz");
        });

        modelBuilder.Entity<WadBuilding>(entity =>
        {
            entity.ToTable("mst_wad_bangunan_air");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.KodeAset).IsUnique();
            entity.HasIndex(x => new { x.Wilayah, x.SortOrder });
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.KodeAset).HasColumnName("kode_aset").HasMaxLength(40);
            entity.Property(x => x.Wilayah).HasColumnName("wilayah").HasMaxLength(40);
            entity.Property(x => x.Kanal).HasColumnName("kanal").HasMaxLength(150);
            entity.Property(x => x.NamaBangunan).HasColumnName("nama_bangunan").HasMaxLength(200);
            entity.Property(x => x.UkuranDimensi).HasColumnName("ukuran_dimensi").HasMaxLength(80);
            entity.Property(x => x.JumlahPapan).HasColumnName("jumlah_papan");
            entity.Property(x => x.JenisBangunan).HasColumnName("jenis_bangunan").HasMaxLength(40);
            entity.Property(x => x.RefTmakLokasi).HasColumnName("ref_tmak_lokasi").HasMaxLength(150);
            entity.Property(x => x.RefTpmatLokasi).HasColumnName("ref_tpmat_lokasi").HasMaxLength(150);
            entity.Property(x => x.Aktif).HasColumnName("aktif");
            entity.Property(x => x.KondisiTerakhir).HasColumnName("kondisi_terakhir").HasMaxLength(250);
            entity.Property(x => x.SortOrder).HasColumnName("sort_order");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
        });

        modelBuilder.Entity<WadOverflowLog>(entity =>
        {
            entity.ToTable("trx_wad_log_overflow");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.KodeAset, x.TanggalLog });
            entity.HasIndex(x => x.RequestId).IsUnique();
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.KodeAset).HasColumnName("kode_aset").HasMaxLength(40);
            entity.Property(x => x.Minggu).HasColumnName("minggu").HasMaxLength(5);
            entity.Property(x => x.TanggalLog).HasColumnName("tanggal_log");
            entity.Property(x => x.StatusOperasi).HasColumnName("status_operasi").HasMaxLength(10);
            entity.Property(x => x.DetailBukaan).HasColumnName("detail_bukaan").HasMaxLength(250);
            entity.Property(x => x.KondisiFisik).HasColumnName("kondisi_fisik").HasMaxLength(250);
            entity.Property(x => x.PetugasNama).HasColumnName("petugas_nama").HasMaxLength(150);
            entity.Property(x => x.PemeriksaNama).HasColumnName("pemeriksa_nama").HasMaxLength(150);
            entity.Property(x => x.RequestId).HasColumnName("request_id");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
            entity.HasOne(x => x.Building)
                .WithMany(x => x.Logs)
                .HasForeignKey(x => x.KodeAset)
                .HasPrincipalKey(x => x.KodeAset)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Photos)
                .WithOne(x => x.Log)
                .HasForeignKey(x => x.LogId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WadOverflowLogPhoto>(entity =>
        {
            entity.ToTable("trx_wad_log_overflow_foto");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.LogId);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.LogId).HasColumnName("log_id");
            entity.Property(x => x.FileUrl).HasColumnName("file_url").HasMaxLength(500);
            entity.Property(x => x.Keterangan).HasColumnName("keterangan").HasMaxLength(200);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
        });

        modelBuilder.Entity<WadSubsidenObservation>(entity =>
        {
            entity.ToTable("trx_wad_subsiden");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.KodePatok, x.Periode }).IsUnique();
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.KodePatok).HasColumnName("kode_patok").HasMaxLength(60);
            entity.Property(x => x.LintangRaw).HasColumnName("lintang_raw").HasMaxLength(40);
            entity.Property(x => x.Lintang).HasColumnName("lintang").HasPrecision(10, 6);
            entity.Property(x => x.BujurRaw).HasColumnName("bujur_raw").HasMaxLength(40);
            entity.Property(x => x.Bujur).HasColumnName("bujur").HasPrecision(10, 6);
            entity.Property(x => x.Wilayah).HasColumnName("wilayah").HasMaxLength(40);
            entity.Property(x => x.Blok).HasColumnName("blok").HasMaxLength(60);
            entity.Property(x => x.TahunTanam).HasColumnName("tahun_tanam");
            entity.Property(x => x.KedalamanGambut).HasColumnName("kedalaman_gambut").HasMaxLength(30);
            entity.Property(x => x.Periode).HasColumnName("periode").HasMaxLength(10);
            entity.Property(x => x.PeriodeLabel).HasColumnName("periode_label").HasMaxLength(40);
            entity.Property(x => x.NilaiCm).HasColumnName("nilai_cm").HasPrecision(10, 2);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
        });

        modelBuilder.Entity<WadSubsidenPhoto>(entity =>
        {
            entity.ToTable("trx_wad_subsiden_foto");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.KodePatok, x.Periode });
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.KodePatok).HasColumnName("kode_patok").HasMaxLength(60);
            entity.Property(x => x.Periode).HasColumnName("periode").HasMaxLength(10);
            entity.Property(x => x.FileUrl).HasColumnName("file_url").HasMaxLength(500);
            entity.Property(x => x.Keterangan).HasColumnName("keterangan").HasMaxLength(200);
            entity.Property(x => x.SortOrder).HasColumnName("sort_order");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
        });

        modelBuilder.Entity<WadSiponInspection>(entity =>
        {
            entity.ToTable("trx_wad_sipon");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Persil).HasColumnName("persil").HasMaxLength(80);
            entity.Property(x => x.Lintang).HasColumnName("lintang").HasPrecision(10, 6);
            entity.Property(x => x.Bujur).HasColumnName("bujur").HasPrecision(10, 6);
            entity.Property(x => x.Tanggal).HasColumnName("tanggal");
            entity.Property(x => x.Kondisi).HasColumnName("kondisi").HasMaxLength(4);
            entity.Property(x => x.TmaInletCm).HasColumnName("tma_inlet_cm").HasPrecision(8, 2);
            entity.Property(x => x.TmaOutletCm).HasColumnName("tma_outlet_cm").HasPrecision(8, 2);
            entity.Property(x => x.TanggulInlet).HasColumnName("tanggul_inlet").HasMaxLength(4);
            entity.Property(x => x.TanggulOutlet).HasColumnName("tanggul_outlet").HasMaxLength(4);
            entity.Property(x => x.Komponen).HasColumnName("komponen").HasColumnType("jsonb");
            entity.Property(x => x.Keterangan).HasColumnName("keterangan").HasMaxLength(1000);
            entity.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(150);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
        });

        modelBuilder.Entity<WadFloodedLandObservation>(entity =>
        {
            entity.ToTable("trx_wad_lahan_tergenang");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Persil).HasColumnName("persil").HasMaxLength(80);
            entity.Property(x => x.Lintang).HasColumnName("lintang").HasPrecision(10, 6);
            entity.Property(x => x.Bujur).HasColumnName("bujur").HasPrecision(10, 6);
            entity.Property(x => x.Tanggal).HasColumnName("tanggal");
            entity.Property(x => x.Kondisi).HasColumnName("kondisi").HasMaxLength(4);
            entity.Property(x => x.TmatCm).HasColumnName("tmat_cm").HasPrecision(8, 2);
            entity.Property(x => x.LuasHa).HasColumnName("luas_ha").HasPrecision(10, 4);
            entity.Property(x => x.AksesJalan).HasColumnName("akses_jalan").HasMaxLength(4);
            entity.Property(x => x.Tanggul).HasColumnName("tanggul").HasMaxLength(4);
            entity.Property(x => x.Keterangan).HasColumnName("keterangan").HasMaxLength(1000);
            entity.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(150);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
        });
    }
}
