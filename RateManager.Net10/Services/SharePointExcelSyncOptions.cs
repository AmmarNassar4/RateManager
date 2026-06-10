namespace RateManager.Net10.Services;

public class SharePointExcelSyncOptions
{
    public bool Enabled { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string DriveId { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int RatePlanId { get; set; }
    public int? DiscountRatePlanId { get; set; }
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public bool UseTodayAsStartDate { get; set; }
    public int NumberOfDays { get; set; } = 30;
    public int DefaultGuestCount { get; set; } = 1;
    public int DefaultRoomCount { get; set; } = 1;
    public int CheckEveryMinutes { get; set; } = 5;
}
