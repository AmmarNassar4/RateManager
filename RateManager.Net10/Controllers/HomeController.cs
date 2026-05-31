using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RateManager.Net10.Data;

namespace RateManager.Net10.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _db;

    public HomeController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.BatchCount = await _db.RateGenerationBatches.CountAsync();
        ViewBag.DailyRateCount = await _db.DailyRoomRates.CountAsync();
        ViewBag.RoomCount = await _db.RoomTypes.CountAsync(x => x.IsActive);
        ViewBag.RatePlanCount = await _db.RatePlans.CountAsync(x => x.IsActive);

        var latestBatches = await _db.RateGenerationBatches
            .Include(x => x.RatePlan)
            .OrderByDescending(x => x.CreatedAt)
            .Take(10)
            .ToListAsync();

        return View(latestBatches);
    }

    public IActionResult Error()
    {
        return View();
    }
}
