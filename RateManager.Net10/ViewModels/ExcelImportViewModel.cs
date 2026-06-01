using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace RateManager.Net10.ViewModels;

public class ExcelImportViewModel
{
    // Rate plan that receives the regular / without-discount prices.
    public int RatePlanId { get; set; }

    // Optional rate plan that receives the discounted prices from the same Excel file.
    public int? DiscountRatePlanId { get; set; }

    // Used only when the Excel file does not contain a detectable discounted section.
    public decimal DiscountPercent { get; set; } = 25;

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
