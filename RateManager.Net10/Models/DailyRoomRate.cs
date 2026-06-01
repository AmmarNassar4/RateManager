namespace RateManager.Net10.Models;

public class DailyRoomRate
{
    public long DailyRoomRateId { get; set; }
    public int RateGenerationBatchId { get; set; }
    public int RatePlanId { get; set; }
    public int RoomTypeId { get; set; }

    public DateOnly RateDate { get; set; }
    public string DayName { get; set; } = string.Empty;
    public bool IsWeekend { get; set; }

    public int GuestCount { get; set; }
    public int RoomCount { get; set; }

    public decimal BaseRate { get; set; }
    public decimal TotalAdjustmentPercent { get; set; }
    public decimal CalculatedRate { get; set; }
    public decimal? ManualRate { get; set; }
    public decimal FinalRate { get; set; }

    public bool IsManualOverride { get; set; }
    public string? CalculationNote { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public RateGenerationBatch? RateGenerationBatch { get; set; }
    public RatePlan? RatePlan { get; set; }
    public RoomType? RoomType { get; set; }
    public ICollection<RateOverride> RateOverrides { get; set; } = new List<RateOverride>();
}
