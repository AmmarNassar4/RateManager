using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace RateManager.Net10.ViewModels;

public class ExcelImportViewModel
{
    public int RatePlanId { get; set; }

    // Main import option: select the Excel file from the browser.
    public IFormFile? ExcelFile { get; set; }

    // Optional fallback: server-side file path.
    // Kept for compatibility with the previous version.
    public string ExcelFilePath { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public int NumberOfDays { get; set; } = 30;
    public int DefaultGuestCount { get; set; } = 2;
    public int DefaultRoomCount { get; set; } = 1;
    public string? Notes { get; set; }

    public List<SelectListItem> RatePlans { get; set; } = new();
}
