namespace RateManager.Net10.Models;

public class MealPriceSetting
{
    public int MealPriceSettingId { get; set; }
    public decimal BreakfastPrice { get; set; }
    public decimal LunchPrice { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
