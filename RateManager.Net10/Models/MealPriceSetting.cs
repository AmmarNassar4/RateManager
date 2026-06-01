namespace RateManager.Net10.Models;

public class MealPriceSetting
{
    public int MealPriceSettingId { get; set; }
    public decimal BreakfastPrice { get; set; }
    public decimal LunchPrice { get; set; }
    public decimal TaxPercent { get; set; } = 15;
    public decimal ChildBreakfastDiscountPercent { get; set; } = 50;
    public decimal ChildLunchDiscountPercent { get; set; } = 50;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
