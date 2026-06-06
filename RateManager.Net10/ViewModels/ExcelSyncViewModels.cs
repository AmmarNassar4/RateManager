namespace RateManager.Net10.ViewModels;

public class ExcelSyncImportRequest
{
    public int RatePlanId { get; set; }
    public int? DiscountRatePlanId { get; set; }
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public int NumberOfDays { get; set; } = 365;
    public int DefaultGuestCount { get; set; } = 2;
    public int DefaultRoomCount { get; set; } = 1;
    public string FileName { get; set; } = "rates.xlsx";
    public string FileContentBase64 { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class ExcelSyncImportResponse
{
    public bool Success { get; set; }
    public int BatchId { get; set; }
    public string Message { get; set; } = string.Empty;
}
