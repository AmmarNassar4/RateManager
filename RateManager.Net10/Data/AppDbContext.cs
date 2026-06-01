using Microsoft.EntityFrameworkCore;
using RateManager.Net10.Models;

using System.Linq;
namespace RateManager.Net10.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<RoomType> RoomTypes => Set<RoomType>();
    public DbSet<RatePlan> RatePlans => Set<RatePlan>();
    public DbSet<RateCalculationRule> RateCalculationRules => Set<RateCalculationRule>();
    public DbSet<RateGenerationBatch> RateGenerationBatches => Set<RateGenerationBatch>();
    public DbSet<DailyRoomRate> DailyRoomRates => Set<DailyRoomRate>();
    public DbSet<RateOverride> RateOverrides => Set<RateOverride>();
    public DbSet<RateAuditLog> RateAuditLogs => Set<RateAuditLog>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<WeekendDaySetting> WeekendDaySettings => Set<WeekendDaySetting>();
    public DbSet<MealPriceSetting> MealPriceSettings => Set<MealPriceSetting>();
    public DbSet<PmsRoomTypeMapping> PmsRoomTypeMappings => Set<PmsRoomTypeMapping>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<RoomType>(entity =>
        {
            entity.HasIndex(x => x.RoomCode).IsUnique();
            entity.Property(x => x.RoomCode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.RoomName).HasMaxLength(150).IsRequired();
        });

        modelBuilder.Entity<RatePlan>(entity =>
        {
            entity.HasIndex(x => x.RatePlanCode).IsUnique();
            entity.Property(x => x.RatePlanCode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.RatePlanName).HasMaxLength(150).IsRequired();
            entity.Property(x => x.MealPlanCode).HasMaxLength(20);
            entity.Property(x => x.CurrencyCode).HasMaxLength(10).IsRequired();
        });

        modelBuilder.Entity<RateCalculationRule>(entity =>
        {
            entity.Property(x => x.RuleName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.PercentageValue).HasColumnType("decimal(9,4)");
            entity.Property(x => x.StartDate).HasConversion(
                value => value.HasValue ? value.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                value => value.HasValue ? DateOnly.FromDateTime(value.Value) : (DateOnly?)null);
            entity.Property(x => x.EndDate).HasConversion(
                value => value.HasValue ? value.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                value => value.HasValue ? DateOnly.FromDateTime(value.Value) : (DateOnly?)null);
        });

        modelBuilder.Entity<RateGenerationBatch>(entity =>
        {
            entity.Property(x => x.StartDate).HasConversion<DateOnlyConverter>();
            entity.Property(x => x.EndDate).HasConversion<DateOnlyConverter>();
            entity.Property(x => x.GlobalAdjustmentPercent).HasColumnType("decimal(9,4)");
            entity.Property(x => x.WeekendAdjustmentPercent).HasColumnType("decimal(9,4)");
            entity.Property(x => x.SourceFilePath).HasMaxLength(500);
            entity.Property(x => x.CreatedBy).HasMaxLength(100);
        });

        modelBuilder.Entity<DailyRoomRate>(entity =>
        {
            entity.Property(x => x.RateDate).HasConversion<DateOnlyConverter>();
            entity.Property(x => x.DayName).HasMaxLength(20).IsRequired();
            entity.Property(x => x.BaseRate).HasColumnType("decimal(18,3)");
            entity.Property(x => x.TotalAdjustmentPercent).HasColumnType("decimal(9,4)");
            entity.Property(x => x.CalculatedRate).HasColumnType("decimal(18,3)");
            entity.Property(x => x.ManualRate).HasColumnType("decimal(18,3)");
            entity.Property(x => x.FinalRate).HasColumnType("decimal(18,3)");
            entity.Property(x => x.CreatedBy).HasMaxLength(100);
            entity.Property(x => x.UpdatedBy).HasMaxLength(100);

            entity.HasIndex(x => new
            {
                x.RateGenerationBatchId,
                x.RatePlanId,
                x.RoomTypeId,
                x.RateDate,
                x.GuestCount,
                x.RoomCount
            }).IsUnique();
        });

        modelBuilder.Entity<RateOverride>(entity =>
        {
            entity.Property(x => x.OldRate).HasColumnType("decimal(18,3)");
            entity.Property(x => x.NewRate).HasColumnType("decimal(18,3)");
            entity.Property(x => x.CreatedBy).HasMaxLength(100);
        });

        modelBuilder.Entity<RateAuditLog>(entity =>
        {
            entity.Property(x => x.EntityName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ActionType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.FieldName).HasMaxLength(100);
            entity.Property(x => x.CreatedBy).HasMaxLength(100);
        });

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasIndex(x => x.UserName).IsUnique();
            entity.Property(x => x.UserName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Role).HasConversion<string>().HasMaxLength(20).IsRequired();
        });

        modelBuilder.Entity<WeekendDaySetting>(entity =>
        {
            entity.HasIndex(x => x.Weekday).IsUnique();
            entity.Property(x => x.Weekday).HasConversion<string>().HasMaxLength(20).IsRequired();
        });

        modelBuilder.Entity<MealPriceSetting>(entity =>
        {
            entity.Property(x => x.BreakfastPrice).HasColumnType("decimal(18,3)");
            entity.Property(x => x.LunchPrice).HasColumnType("decimal(18,3)");
            entity.Property(x => x.TaxPercent).HasColumnType("decimal(9,4)");
            entity.Property(x => x.ChildBreakfastDiscountPercent).HasColumnType("decimal(9,4)");
            entity.Property(x => x.ChildLunchDiscountPercent).HasColumnType("decimal(9,4)");
        });

        modelBuilder.Entity<PmsRoomTypeMapping>(entity =>
        {
            entity.HasIndex(x => x.RoomTypeId).IsUnique();
            entity.Property(x => x.PmsRoomTypeCode).HasMaxLength(20).IsRequired();
        });

        foreach (var foreignKey in modelBuilder.Model.GetEntityTypes().SelectMany(entityType => entityType.GetForeignKeys()))
        {
            foreignKey.DeleteBehavior = DeleteBehavior.NoAction;
        }
    }
}
