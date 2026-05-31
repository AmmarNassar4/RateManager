using RateManager.Net10.Models;

namespace RateManager.Net10.Data;

public static class SeedData
{
    public static void Initialize(AppDbContext db)
    {
        if (!db.RoomTypes.Any())
        {
            db.RoomTypes.AddRange(
                new RoomType { RoomCode = "STD-KING", RoomName = "Standard King Room" },
                new RoomType { RoomCode = "STD-TWIN", RoomName = "Standard Twin Room" },
                new RoomType { RoomCode = "JUNIOR-SUITE", RoomName = "Junior Suite" },
                new RoomType { RoomCode = "EXEC-SUITE", RoomName = "Executive Suite" },
                new RoomType { RoomCode = "ELITE-SUITE", RoomName = "Elite Suite" }
            );
        }

        if (!db.RatePlans.Any())
        {
            db.RatePlans.AddRange(
                new RatePlan { RatePlanCode = "WALK-IN", RatePlanName = "Walk-In Rates", MealPlanCode = "RO", CurrencyCode = "SAR" },
                new RatePlan { RatePlanCode = "FAMILY-25", RatePlanName = "Family and Friends 25%", MealPlanCode = "RO", CurrencyCode = "SAR" }
            );
        }

        db.SaveChanges();
    }
}
