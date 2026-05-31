using RateManager.Net10.Models;

namespace RateManager.Net10.ViewModels;

public class CalendarViewModel
{
    public RateGenerationBatch Batch { get; set; } = new();
    public List<DailyRoomRate> Rates { get; set; } = new();
}

public class ManualRateUpdateInput
{
    public long DailyRoomRateId { get; set; }
    public decimal? ManualRate { get; set; }
    public string? Reason { get; set; }
}
