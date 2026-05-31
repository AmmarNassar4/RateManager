using RateManager.Net10.Models;

namespace RateManager.Net10.ViewModels;

public class SettingsViewModel
{
    public List<RoomType> RoomTypes { get; set; } = new();
    public List<RatePlan> RatePlans { get; set; } = new();
}

public class CreateRoomTypeInput
{
    public string RoomCode { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
}

public class CreateRatePlanInput
{
    public string RatePlanCode { get; set; } = string.Empty;
    public string RatePlanName { get; set; } = string.Empty;
    public string? MealPlanCode { get; set; }
    public string CurrencyCode { get; set; } = "SAR";
}
