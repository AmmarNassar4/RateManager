using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RateManager.Net10.Data;
using RateManager.Net10.Models;
using RateManager.Net10.Services;
using RateManager.Net10.ViewModels;

namespace RateManager.Net10.Controllers;

[Authorize(Roles = "Admin")]
public class SettingsController : Controller
{
    private readonly AppDbContext _db;

    public SettingsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        await EnsureWeekendRowsAsync();
        await EnsureMealPriceSettingsAsync();

        var model = new SettingsViewModel
        {
            RoomTypes = await _db.RoomTypes.OrderBy(x => x.RoomName).ToListAsync(),
            RatePlans = await _db.RatePlans.OrderBy(x => x.RatePlanName).ToListAsync(),
            Users = await _db.AppUsers.OrderBy(x => x.UserName).ToListAsync(),
            WeekendDays = await _db.WeekendDaySettings.OrderBy(x => x.Weekday).ToListAsync(),
            MealPrices = await _db.MealPriceSettings.OrderBy(x => x.MealPriceSettingId).FirstAsync()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddRoomType(CreateRoomTypeInput input)
    {
        if (!string.IsNullOrWhiteSpace(input.RoomCode) && !string.IsNullOrWhiteSpace(input.RoomName))
        {
            _db.RoomTypes.Add(new RoomType
            {
                RoomCode = input.RoomCode.Trim().ToUpperInvariant(),
                RoomName = input.RoomName.Trim()
            });

            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddRatePlan(CreateRatePlanInput input)
    {
        if (!string.IsNullOrWhiteSpace(input.RatePlanCode) && !string.IsNullOrWhiteSpace(input.RatePlanName))
        {
            _db.RatePlans.Add(new RatePlan
            {
                RatePlanCode = input.RatePlanCode.Trim().ToUpperInvariant(),
                RatePlanName = input.RatePlanName.Trim(),
                MealPlanCode = input.MealPlanCode?.Trim(),
                CurrencyCode = string.IsNullOrWhiteSpace(input.CurrencyCode) ? "SAR" : input.CurrencyCode.Trim().ToUpperInvariant()
            });

            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddUser(CreateUserInput input)
    {
        if (!string.IsNullOrWhiteSpace(input.UserName) && !string.IsNullOrWhiteSpace(input.Password))
        {
            var userName = input.UserName.Trim();
            var exists = await _db.AppUsers.AnyAsync(x => x.UserName == userName);
            if (!exists)
            {
                _db.AppUsers.Add(new AppUser
                {
                    UserName = userName,
                    PasswordHash = PasswordHasher.HashPassword(input.Password),
                    Role = input.Role,
                    IsActive = true
                });

                await _db.SaveChangesAsync();
            }
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateWeekendDays(UpdateWeekendDaysInput input)
    {
        await EnsureWeekendRowsAsync();
        var selected = input.WeekendDays.ToHashSet();
        var rows = await _db.WeekendDaySettings.ToListAsync();

        foreach (var row in rows)
        {
            row.Enabled = selected.Contains(row.Weekday);
        }

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateMealPrices(UpdateMealPricesInput input)
    {
        await EnsureMealPriceSettingsAsync();
        var settings = await _db.MealPriceSettings.OrderBy(x => x.MealPriceSettingId).FirstAsync();
        settings.BreakfastPrice = input.BreakfastPrice;
        settings.LunchPrice = input.LunchPrice;
        settings.TaxPercent = input.TaxPercent;
        settings.ChildBreakfastDiscountPercent = input.ChildBreakfastDiscountPercent;
        settings.ChildLunchDiscountPercent = input.ChildLunchDiscountPercent;
        settings.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private async Task EnsureWeekendRowsAsync()
    {
        foreach (DayOfWeek day in Enum.GetValues<DayOfWeek>())
        {
            if (!await _db.WeekendDaySettings.AnyAsync(x => x.Weekday == day))
            {
                _db.WeekendDaySettings.Add(new WeekendDaySetting
                {
                    Weekday = day,
                    Enabled = day is DayOfWeek.Friday or DayOfWeek.Saturday
                });
            }
        }

        await _db.SaveChangesAsync();
    }

    private async Task EnsureMealPriceSettingsAsync()
    {
        if (!await _db.MealPriceSettings.AnyAsync())
        {
            _db.MealPriceSettings.Add(new MealPriceSetting
            {
                BreakfastPrice = 50,
                LunchPrice = 100,
                TaxPercent = 15,
                ChildBreakfastDiscountPercent = 50,
                ChildLunchDiscountPercent = 50
            });

            await _db.SaveChangesAsync();
        }
    }
}
