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
    }
}