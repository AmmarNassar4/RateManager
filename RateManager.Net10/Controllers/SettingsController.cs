using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RateManager.Net10.Data;
using RateManager.Net10.Models;
using RateManager.Net10.ViewModels;

namespace RateManager.Net10.Controllers;

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
        var model = new SettingsViewModel
        {
            RoomTypes = await _db.RoomTypes.OrderBy(x => x.RoomName).ToListAsync(),
            RatePlans = await _db.RatePlans.OrderBy(x => x.RatePlanName).ToListAsync()
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
}
