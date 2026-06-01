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
            ?? throw new InvalidOperationException("Pricing settings were not found.");

        var childBreakfastPrice = CalculateChildMealPrice(mealPrices.BreakfastPrice, mealPrices.ChildBreakfastDiscountPercent);
        var childLunchPrice = CalculateChildMealPrice(mealPrices.LunchPrice, mealPrices.ChildLunchDiscountPercent);

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
            ChildBreakfastPrice = childBreakfastPrice,
            ChildBreakfastPeople = request.IncludeBreakfast ? Math.Max(0, request.ChildBreakfastPeople) : 0,
            LunchPrice = mealPrices.LunchPrice,
            LunchPeople = request.IncludeLunch ? Math.Max(0, request.LunchPeople) : 0,
            ChildLunchPrice = childLunchPrice,
            ChildLunchPeople = request.IncludeLunch ? Math.Max(0, request.ChildLunchPeople) : 0,
            TaxPercent = mealPrices.TaxPercent
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
        response.ChildBreakfastTotal = response.ChildBreakfastPrice * response.ChildBreakfastPeople * request.Nights;
        response.LunchTotal = response.LunchPrice * response.LunchPeople * request.Nights;
        response.ChildLunchTotal = response.ChildLunchPrice * response.ChildLunchPeople * request.Nights;
        response.MealTotal = response.BreakfastTotal + response.ChildBreakfastTotal + response.LunchTotal + response.ChildLunchTotal;
        response.Subtotal = response.RoomTotal + response.MealTotal;
        response.TaxAmount = Math.Round(response.Subtotal * response.TaxPercent / 100m, 3, MidpointRounding.AwayFromZero);
        response.GrandTotal = response.Subtotal + response.TaxAmount;

        return response;
    }

    private static decimal CalculateChildMealPrice(decimal adultMealPrice, decimal discountPercent)
    {
        var normalizedDiscount = Math.Clamp(discountPercent, 0, 100);
        return Math.Round(adultMealPrice * (1 - normalizedDiscount / 100m), 3, MidpointRounding.AwayFromZero);
    }
}
