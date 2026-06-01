using Microsoft.EntityFrameworkCore;
using RateManager.Net10.Data;
using RateManager.Net10.ViewModels;

namespace RateManager.Net10.Services;

public class CurrentRateCalculatorService : ICurrentRateCalculatorService
{
    private readonly AppDbContext _db;

    public CurrentRateCalculatorService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<CurrentRateCalculationResponse> CalculateAsync(CurrentRateCalculationRequest request)
    {
        if (request.Nights <= 0)
        {
            throw new InvalidOperationException("Nights must be greater than zero.");
        }

        if (request.RoomCount <= 0)
        {
            throw new InvalidOperationException("Room count must be greater than zero.");
        }

        var ratePlan = await _db.RatePlans.FirstOrDefaultAsync(x => x.RatePlanId == request.RatePlanId && x.IsActive)
            ?? throw new InvalidOperationException("Rate plan was not found.");

        var roomType = await _db.RoomTypes.FirstOrDefaultAsync(x => x.RoomTypeId == request.RoomTypeId && x.IsActive)
            ?? throw new InvalidOperationException("Room type was not found.");

        var mealPrices = await _db.MealPriceSettings.OrderBy(x => x.MealPriceSettingId).FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("Meal price settings were not found.");

        var response = new CurrentRateCalculationResponse
        {
            RatePlanId = ratePlan.RatePlanId,
            RatePlanName = ratePlan.RatePlanName,
            RoomTypeId = roomType.RoomTypeId,
            RoomTypeName = roomType.RoomName,
            StartDate = request.StartDate,
            Nights = request.Nights,
            RoomCount = request.RoomCount,
            BreakfastPrice = mealPrices.BreakfastPrice,
            BreakfastPeople = request.IncludeBreakfast ? Math.Max(0, request.BreakfastPeople) : 0,
            LunchPrice = mealPrices.LunchPrice,
            LunchPeople = request.IncludeLunch ? Math.Max(0, request.LunchPeople) : 0
        };

        for (var offset = 0; offset < request.Nights; offset++)
        {
            var date = request.StartDate.AddDays(offset);
            var dailyRate = await _db.DailyRoomRates
                .Where(x => x.RatePlanId == request.RatePlanId && x.RoomTypeId == request.RoomTypeId && x.RateDate == date)
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.DailyRoomRateId)
                .FirstOrDefaultAsync();

            if (dailyRate == null)
            {
                throw new InvalidOperationException($"No rate was found for {date:yyyy-MM-dd}.");
            }

            var roomRate = dailyRate.FinalRate;
            var roomTotal = roomRate * request.RoomCount;

            response.NightsBreakdown.Add(new CurrentRateNightLine
            {
                Date = date,
                RoomRate = roomRate,
                RoomCount = request.RoomCount,
                RoomTotal = roomTotal
            });

            response.RoomTotal += roomTotal;
        }

        response.BreakfastTotal = response.BreakfastPrice * response.BreakfastPeople * request.Nights;
        response.LunchTotal = response.LunchPrice * response.LunchPeople * request.Nights;
        response.GrandTotal = response.RoomTotal + response.BreakfastTotal + response.LunchTotal;

        return response;
    }
}
