namespace RateManager.Net10.Models;

public enum RateRuleScope
{
    General = 1,
    RoomType = 2,
    Weekday = 3,
    DateRange = 4
}

public class RateCalculationRule
{
    public int RateCalculationRuleId { get; set; }
    public int RatePlanId { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public RateRuleScope RuleScope { get; set; } = RateRuleScope.General;
    public decimal PercentageValue { get; set; }

    public int? RoomTypeId { get; set; }
    public DayOfWeek? Weekday { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public RatePlan? RatePlan { get; set; }
    public RoomType? RoomType { get; set; }
}
