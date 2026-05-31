namespace RateManager.Net10.Models;

public enum RateSourceType
{
    Calculation = 1,
    ExcelImport = 2
}

public class RateGenerationBatch
{
    public int RateGenerationBatchId { get; set; }
    public int RatePlanId { get; set; }
    public RateSourceType SourceType { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int NumberOfDays { get; set; }
    public decimal GlobalAdjustmentPercent { get; set; }
    public string? SourceFilePath { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }

    public RatePlan? RatePlan { get; set; }
    public ICollection<DailyRoomRate> DailyRoomRates { get; set; } = new List<DailyRoomRate>();
}
