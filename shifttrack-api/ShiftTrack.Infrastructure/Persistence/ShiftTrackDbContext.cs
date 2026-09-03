using Microsoft.EntityFrameworkCore;
using ShiftTrack.Domain.Entities;

namespace ShiftTrack.Infrastructure.Persistence;

public sealed class ShiftTrackDbContext(DbContextOptions<ShiftTrackDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserSchedulePeriod> UserSchedulePeriods => Set<UserSchedulePeriod>();
    public DbSet<UserScheduleOverride> UserScheduleOverrides => Set<UserScheduleOverride>();
    public DbSet<SwapRequest> SwapRequests => Set<SwapRequest>();
    public DbSet<PtoRequest> PtoRequests => Set<PtoRequest>();
    public DbSet<ScheduleEvent> ScheduleEvents => Set<ScheduleEvent>();
    public DbSet<WeeklyCoverageSnapshot> WeeklyCoverageSnapshots => Set<WeeklyCoverageSnapshot>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<CompanyCatalogItem> Companies => Set<CompanyCatalogItem>();
    public DbSet<CompanyOperationItem> CompanyOperations => Set<CompanyOperationItem>();
    public DbSet<CoverageRule> CoverageRules => Set<CoverageRule>();
    public DbSet<ResetToken> ResetTokens => Set<ResetToken>();
    public DbSet<RequestExportJob> RequestExportJobs => Set<RequestExportJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users", "dbo");
            entity.HasKey(item => item.Id).HasName("PK_Users");
            entity.Ignore(item => item.SchedulePeriods);
            entity.Property(item => item.ObjectId).HasConversion<string>().HasMaxLength(36).IsRequired();
            entity.Property(item => item.Email).HasMaxLength(320).IsRequired();
            entity.Property(item => item.DisplayName).HasMaxLength(200);
            entity.Property(item => item.PasswordHash).HasMaxLength(500);
            entity.Property(item => item.Location).HasMaxLength(50).IsRequired();
            entity.Property(item => item.Company).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Operation).HasMaxLength(120).IsRequired();
            entity.Property(item => item.ShiftTime).HasMaxLength(50).IsRequired();
            entity.HasIndex(item => item.Email).IsUnique().HasDatabaseName("UX_Users_Email");
            entity.HasIndex(item => item.ObjectId).HasDatabaseName("IX_Users_ObjectId");
            entity.HasIndex(item => item.IsActive).HasDatabaseName("IX_Users_IsActive");
        });

        modelBuilder.Entity<UserSchedulePeriod>(entity =>
        {
            entity.ToTable("UserSchedulePeriods", "dbo");
            entity.HasKey(item => item.Id).HasName("PK_UserSchedulePeriods");
            entity.Property(item => item.EffectiveFrom).HasColumnType("date");
            entity.Property(item => item.EffectiveTo).HasColumnType("date");
            entity.Property(item => item.ShiftTime).HasMaxLength(50).IsRequired();
            entity.Property(item => item.BlocksJson).IsRequired();
            entity.Property(item => item.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(item => item.IsRepeating).HasDefaultValue(false);
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_UserSchedulePeriods_Users");
            entity.HasIndex(item => new { item.UserId, item.EffectiveFrom, item.EffectiveTo, item.CreatedAtUtc })
                .IsDescending(false, true, false, true)
                .HasDatabaseName("IX_UserSchedulePeriods_UserId_EffectiveFrom");
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_UserSchedulePeriods_EffectiveRange",
                "EffectiveTo IS NULL OR EffectiveTo >= EffectiveFrom"));
        });

        modelBuilder.Entity<UserScheduleOverride>(entity =>
        {
            entity.ToTable("UserScheduleOverrides", "dbo");
            entity.HasKey(item => item.Id).HasName("PK_UserScheduleOverrides");
            entity.Property(item => item.OverrideDate).HasColumnType("date");
            entity.Property(item => item.EntryType).HasMaxLength(40).IsRequired();
            entity.Property(item => item.RequestType).HasMaxLength(40);
            entity.Property(item => item.Comments).HasMaxLength(1000);
            entity.Property(item => item.StartTime).HasMaxLength(8);
            entity.Property(item => item.EndTime).HasMaxLength(8);
            entity.Property(item => item.Label).HasMaxLength(120);
            entity.HasIndex(item => new { item.UserId, item.OverrideDate }).IsUnique().HasDatabaseName("UX_UserScheduleOverrides_UserDate");
            entity.HasIndex(item => item.OverrideDate).HasDatabaseName("IX_UserScheduleOverrides_OverrideDate");
        });

        modelBuilder.Entity<SwapRequest>(entity =>
        {
            entity.ToTable("SwapRequests", "dbo");
            entity.HasKey(item => item.Id).HasName("PK_SwapRequests");
            entity.Property(item => item.RequestedByEmail).HasMaxLength(320).IsRequired();
            entity.Property(item => item.RequestedByDisplayName).HasMaxLength(200).IsRequired();
            entity.Property(item => item.TargetUserEmail).HasMaxLength(320).IsRequired();
            entity.Property(item => item.TargetUserDisplayName).HasMaxLength(200).IsRequired();
            entity.Property(item => item.SwapDate).HasColumnType("date");
            entity.Property(item => item.RequestType).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Comments).HasMaxLength(1000);
            entity.Property(item => item.ReviewComments).HasMaxLength(1000);
            entity.Property(item => item.Status).HasMaxLength(20).IsRequired();
            entity.Property(item => item.ReviewedByEmail).HasMaxLength(320);
            entity.Property(item => item.ReviewedByName).HasMaxLength(200);
            entity.Property(item => item.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(item => item.UpdatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(item => item.RequestedDatesJson).HasDefaultValue("[]").IsRequired();
            entity.Property(item => item.TargetDatesJson).HasDefaultValue("[]").IsRequired();
            entity.Property(item => item.PairingsJson).HasDefaultValue("[]").IsRequired();
            entity.Property(item => item.WeeklyHoursJson).HasDefaultValue("[]").IsRequired();
            entity.HasIndex(item => new { item.Status, item.CreatedAtUtc }).IsDescending(false, true).HasDatabaseName("IX_SwapRequests_Status_CreatedAtUtc");
            entity.HasIndex(item => new { item.TargetUserId, item.Status, item.CreatedAtUtc }).IsDescending(false, false, true).HasDatabaseName("IX_SwapRequests_TargetUserId_Status");
            entity.HasIndex(item => new { item.RequestedByRole, item.Status, item.CreatedAtUtc }).IsDescending(false, false, true).HasDatabaseName("IX_SwapRequests_RequestedByRole_Status");
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_SwapRequests_Status",
                "Status IN ('pending', 'approved', 'denied', 'canceled')"));
        });

        modelBuilder.Entity<PtoRequest>(entity =>
        {
            entity.ToTable("PtoRequests", "dbo");
            entity.HasKey(item => item.Id).HasName("PK_PtoRequests");
            entity.Property(item => item.UserEmail).HasMaxLength(320).IsRequired();
            entity.Property(item => item.UserDisplayName).HasMaxLength(200).IsRequired();
            entity.Property(item => item.RequestType).HasMaxLength(40).IsRequired();
            entity.Property(item => item.StartDate).HasColumnType("date");
            entity.Property(item => item.EndDate).HasColumnType("date");
            entity.Property(item => item.Comments).HasMaxLength(1000);
            entity.Property(item => item.ReviewComments).HasMaxLength(1000);
            entity.Property(item => item.Status).HasMaxLength(20).IsRequired();
            entity.Property(item => item.RequestedByEmail).HasMaxLength(320).IsRequired();
            entity.Property(item => item.RequestedByName).HasMaxLength(200).IsRequired();
            entity.Property(item => item.ReviewedByEmail).HasMaxLength(320);
            entity.Property(item => item.ReviewedByName).HasMaxLength(200);
            entity.HasIndex(item => item.UserId).HasDatabaseName("IX_PtoRequests_UserId");
            entity.HasIndex(item => item.Status).HasDatabaseName("IX_PtoRequests_Status");
            entity.HasIndex(item => item.StartDate).HasDatabaseName("IX_PtoRequests_StartDate");
        });

        modelBuilder.Entity<RequestExportJob>(entity =>
        {
            entity.ToTable("RequestExportJobs", "dbo");
            entity.HasKey(item => item.Id).HasName("PK_RequestExportJobs");
            entity.Property(item => item.RequestedByEmail).HasMaxLength(320).IsRequired();
            entity.Property(item => item.RequestedByName).HasMaxLength(200).IsRequired();
            entity.Property(item => item.ScopeCompaniesJson).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(20).IsRequired();
            entity.Property(item => item.HangfireJobId).HasMaxLength(100);
            entity.Property(item => item.FileName).HasMaxLength(260);
            entity.Property(item => item.ContentType).HasMaxLength(120);
            entity.Property(item => item.ErrorMessage).HasMaxLength(2000);
            entity.Property(item => item.FileContent).HasColumnType("varbinary(max)");
            entity.Property(item => item.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(item => new { item.RequestedByEmail, item.CreatedAtUtc }).IsDescending(false, true).HasDatabaseName("IX_RequestExportJobs_Requester");
            entity.HasIndex(item => new { item.Status, item.CreatedAtUtc }).IsDescending(false, true).HasDatabaseName("IX_RequestExportJobs_Status");
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_RequestExportJobs_Status",
                "Status IN ('pending', 'queued', 'processing', 'completed', 'failed')"));
        });

        modelBuilder.Entity<ScheduleEvent>(entity =>
        {
            entity.ToTable("ScheduleEvents", "dbo");
            entity.HasKey(item => item.Id).HasName("PK_ScheduleEvents");
            entity.Property(item => item.EmployeeEmail).HasMaxLength(320).IsRequired();
            entity.Property(item => item.Action).HasMaxLength(50).IsRequired();
            entity.Property(item => item.UpdatedByEmail).HasMaxLength(320).IsRequired();
            entity.Property(item => item.UpdatedByName).HasMaxLength(200).IsRequired();
            entity.Property(item => item.PayloadJson).IsRequired();
            entity.HasIndex(item => item.OccurredAtUtc).IsDescending().HasDatabaseName("IX_ScheduleEvents_OccurredAtUtc");
        });

        modelBuilder.Entity<WeeklyCoverageSnapshot>(entity =>
        {
            entity.ToTable("WeeklyCoverageSnapshots", "dbo");
            entity.HasKey(item => item.WeekStartDate).HasName("PK_WeeklyCoverageSnapshots");
            entity.Property(item => item.WeekStartDate).HasColumnType("date");
            entity.Property(item => item.PayloadJson).IsRequired();
        });

        modelBuilder.Entity<Holiday>(entity =>
        {
            entity.ToTable("Holidays", "dbo");
            entity.HasKey(item => item.Id).HasName("PK_Holidays");
            entity.Property(item => item.Date).HasColumnType("date");
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.Property(item => item.CountryCode).HasMaxLength(10).HasDefaultValue("CO").IsRequired();
            entity.Property(item => item.IsActive).HasDefaultValue(true);
            entity.Property(item => item.IsManual).HasDefaultValue(false);
            entity.Property(item => item.CreatedAtUtc).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(item => item.UpdatedAtUtc).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(item => new { item.Date, item.CountryCode }).IsUnique().HasDatabaseName("UX_Holidays_Date_CountryCode");
        });

        modelBuilder.Entity<CompanyCatalogItem>(entity =>
        {
            entity.ToTable("Companies", "dbo");
            entity.HasKey(item => item.Name).HasName("PK_Companies");
            entity.Property(item => item.Name).HasMaxLength(200);
            entity.Property(item => item.IsActive).HasDefaultValue(true);
            entity.Property(item => item.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        });

        modelBuilder.Entity<CompanyOperationItem>(entity =>
        {
            entity.ToTable("CompanyOperations", "dbo");
            entity.HasKey(item => new { item.CompanyName, item.Name }).HasName("PK_CompanyOperations");
            entity.Property(item => item.CompanyName).HasMaxLength(200);
            entity.Property(item => item.Name).HasMaxLength(120);
            entity.Property(item => item.IsActive).HasDefaultValue(true);
            entity.Property(item => item.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        });

        modelBuilder.Entity<CoverageRule>(entity =>
        {
            entity.ToTable("CoverageRules", "dbo");
            entity.HasKey(item => item.Id).HasName("PK_CoverageRules");
            entity.Property(item => item.CompanyName).HasMaxLength(200).IsRequired();
            entity.Property(item => item.OperationName).HasMaxLength(200).HasDefaultValue(string.Empty).IsRequired();
            entity.Property(item => item.DayOfWeek).HasConversion<byte>().HasColumnType("tinyint");
            entity.Property(item => item.CalculationScope).HasMaxLength(20).HasDefaultValue("operation").IsRequired();
            entity.Property(item => item.IsActive).HasDefaultValue(true);
            entity.Property(item => item.UpdatedBy).HasMaxLength(320).HasDefaultValue(string.Empty).IsRequired();
            entity.Property(item => item.UpdatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(item => new { item.CompanyName, item.OperationName, item.DayOfWeek })
                .IsUnique()
                .HasDatabaseName("UX_CoverageRules_ScopeDay");
        });

        modelBuilder.Entity<ResetToken>(entity =>
        {
            entity.ToTable("ResetTokens", "dbo");
            entity.HasKey(item => item.Id).HasName("PK_ResetTokens");
            entity.Property(item => item.Email).HasMaxLength(320).IsRequired();
            entity.Property(item => item.TokenHash).HasMaxLength(128).IsRequired();
            entity.Property(item => item.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(item => new { item.Email, item.UsedAtUtc, item.ExpiresAt })
                .HasDatabaseName("IX_ResetTokens_Email_UsedAtUtc_ExpiresAt");
        });
    }
}
