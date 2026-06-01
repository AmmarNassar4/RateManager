namespace RateManager.Net10.Models;

public class WeekendDaySetting
{
    public int WeekendDaySettingId { get; set; }
    public DayOfWeek Weekday { get; set; }
    public bool Enabled { get; set; }
}
