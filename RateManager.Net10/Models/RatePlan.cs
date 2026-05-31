namespace RateManager.Net10.Models;

public class RatePlan
{
    public int RatePlanId { get; set; }
    public string RatePlanCode { get; set; } = string.Empty;
    public string RatePlanName { get; set; } = string.Empty;
    public string? MealPlanCode { get; set; }
    public string CurrencyCode { get; set; } = "SAR";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<DailyRoomRate> DailyRoomRates { get; set; } = new List<DailyRoomRate>();
    public ICollection<RateCalculationRule> CalculationRules { get; set; } = new List<RateCalculationRule>();
}
