using Microsoft.AspNetCore.Mvc.Rendering;

namespace RateManager.Net10.ViewModels;

public class GenerateRatesViewModel
{
    public int RatePlanId { get; set; }
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public int NumberOfDays { get; set; } = 30;
    public decimal GlobalAdjustmentPercent { get; set; }
    public decimal WeekendAdjustmentPercent { get; set; }
    public string? Notes { get; set; }

    public List<RoomRateInputModel> Rooms { get; set; } = new();
    public List<SelectListItem> RatePlans { get; set; } = new();
    public List<SelectListItem> RoomTypes { get; set; } = new();
}

public class RoomRateInputModel
{
    public int RoomTypeId { get; set; }
    public decimal BaseRate { get; set; }
    public decimal RoomAdjustmentPercent { get; set; }
    public int GuestCount { get; set; } = 2;
    public int RoomCount { get; set; } = 1;
}
