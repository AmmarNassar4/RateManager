using RateManager.Net10.Models;
using RateManager.Net10.Services;

namespace RateManager.Net10.Data;

public static class SeedData
{
    public static void Initialize(AppDbContext db)
    {
        if (!db.RoomTypes.Any())
        {
            db.RoomTypes.AddRange(
                //new RoomType { RoomCode = "STD-KING", RoomName = "Standard King Room" },
                //new RoomType { RoomCode = "STD-TWIN", RoomName = "Standard Twin Room" },
                //new RoomType { RoomCode = "JUNIOR-SUITE", RoomName = "Junior Suite" },
                //new RoomType { RoomCode = "EXEC-SUITE", RoomName = "Executive Suite" },
                //new RoomType { RoomCode = "ELITE-SUITE", RoomName = "Elite Suite" }
            );
        }

        if (!db.RatePlans.Any())
        {
            db.RatePlans.AddRange(
                new RatePlan { RatePlanCode = "WALK-IN", RatePlanName = "Walk-In Rates", MealPlanCode = "RO", CurrencyCode = "SAR" },
                new RatePlan { RatePlanCode = "FAMILY-25", RatePlanName = "Family and Friends 25%", MealPlanCode = "RO", CurrencyCode = "SAR" }
            );
        }

        if (!db.AppUsers.Any())
        {
            db.AppUsers.AddRange(
                new AppUser
                {
                    UserName = "admin",
                    PasswordHash = PasswordHasher.HashPassword("Admin@123"),
                    Role = AppUserRole.Admin,
                    IsActive = true
                },
                new AppUser
                {
                    UserName = "viewer",
                    PasswordHash = PasswordHasher.HashPassword("Viewer@123"),
                    Role = AppUserRole.Viewer,
                    IsActive = true
                }
            );
        }

        foreach (DayOfWeek day in Enum.GetValues<DayOfWeek>())
        {
            if (!db.WeekendDaySettings.Any(x => x.Weekday == day))
            {
                db.WeekendDaySettings.Add(new WeekendDaySetting
                {
                    Weekday = day,
                    Enabled = day is DayOfWeek.Friday or DayOfWeek.Saturday
                });
            }
        }

        if (!db.MealPriceSettings.Any())
        {
            db.MealPriceSettings.Add(new MealPriceSetting { BreakfastPrice = 50, LunchPrice = 100 });
        }

        db.SaveChanges();
    }
}
