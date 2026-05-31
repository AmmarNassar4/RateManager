namespace RateManager.Net10.Models;

public class RateOverride
{
    public long RateOverrideId { get; set; }
    public long DailyRoomRateId { get; set; }
    public decimal OldRate { get; set; }
    public decimal NewRate { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }

    public DailyRoomRate? DailyRoomRate { get; set; }
}
