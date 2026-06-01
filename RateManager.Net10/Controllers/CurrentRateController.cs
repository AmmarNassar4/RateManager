using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RateManager.Net10.Data;
using RateManager.Net10.Services;
using RateManager.Net10.ViewModels;

namespace RateManager.Net10.Controllers;

[Authorize]
public class CurrentRateController : Controller
{
    private readonly AppDbContext _db;
    private readonly ICurrentRateCalculatorService _calculator;

    public CurrentRateController(AppDbContext db, ICurrentRateCalculatorService calculator)
    {
        _db = db;
        _calculator = calculator;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var mealPrices = await _db.MealPriceSettings.OrderBy(x => x.MealPriceSettingId).FirstOrDefaultAsync();
        var breakfastPrice = mealPrices?.BreakfastPrice ?? 0;
        var lunchPrice = mealPrices?.LunchPrice ?? 0;
        var childBreakfastDiscount = mealPrices?.ChildBreakfastDiscountPercent ?? 50;
        var childLunchDiscount = mealPrices?.ChildLunchDiscountPercent ?? 50;

        var model = new CurrentRateCalculatorViewModel
        {
            StartDate = DateOnly.FromDateTime(DateTime.Today),
            Nights = 1,
            RoomCount = 1,
            BreakfastPrice = breakfastPrice,
            LunchPrice = lunchPrice,
            ChildBreakfastDiscountPercent = childBreakfastDiscount,
            ChildLunchDiscountPercent = childLunchDiscount,
            ChildBreakfastPrice = CalculateChildMealPrice(breakfastPrice, childBreakfastDiscount),
            ChildLunchPrice = CalculateChildMealPrice(lunchPrice, childLunchDiscount),
            TaxPercent = mealPrices?.TaxPercent ?? 15,
            RatePlans = await _db.RatePlans
                .Where(x => x.IsActive)
                .OrderByDescending(x => x.RatePlanCode == "WALK-IN" || x.RatePlanName == "Walk-In Rates")
                .ThenBy(x => x.RatePlanName)
                .Select(x => new SelectListItem { Value = x.RatePlanId.ToString(), Text = x.RatePlanName })
                .ToListAsync(),
            RoomTypes = await _db.RoomTypes
                .Where(x => x.IsActive)
                .OrderBy(x => x.RoomName)
                .Select(x => new SelectListItem { Value = x.RoomTypeId.ToString(), Text = x.RoomName })
                .ToListAsync()
        };

        if (model.RatePlanId == 0 && model.RatePlans.Any())
        {
            model.RatePlanId = int.Parse(model.RatePlans.First().Value ?? "0");
        }

        if (model.RoomTypeId == 0 && model.RoomTypes.Any())
        {
            model.RoomTypeId = int.Parse(model.RoomTypes.First().Value ?? "0");
        }

        return View(model);
    }

    [HttpGet("api/current-rate/calculate")]
    public async Task<IActionResult> CalculateApi([FromQuery] CurrentRateCalculationRequest request)
    {
        try
        {
            var result = await _calculator.CalculateAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private static decimal CalculateChildMealPrice(decimal adultMealPrice, decimal discountPercent)
    {
        var normalizedDiscount = Math.Clamp(discountPercent, 0, 100);
        return Math.Round(adultMealPrice * (1 - normalizedDiscount / 100m), 3, MidpointRounding.AwayFromZero);
    }
}
