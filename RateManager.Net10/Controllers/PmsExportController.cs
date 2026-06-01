using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RateManager.Net10.Data;
using RateManager.Net10.Services;
using RateManager.Net10.ViewModels;

namespace RateManager.Net10.Controllers;

[Authorize(Roles = "Admin")]
public class PmsExportController : Controller
{
    private readonly AppDbContext _db;
    private readonly IPmsExportService _exportService;

    public PmsExportController(AppDbContext db, IPmsExportService exportService)
    {
        _db = db;
        _exportService = exportService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        return View(await BuildViewModelAsync(new PmsExportViewModel
        {
            StartDate = DateOnly.FromDateTime(DateTime.Today),
            NumberOfDays = 30,
            ExportRo = true,
            ExportBb = true,
            ExportHb = true,
            ExportFb = false
        }));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(PmsExportViewModel model)
    {
        try
        {
            var result = await _exportService.ExportAsync(model, User.Identity?.Name);
            TempData["ExportSummary"] = $"Inserted: {result.InsertedRows}, Updated: {result.UpdatedRows}, Skipped: {result.SkippedRows}";
            if (result.Messages.Any())
            {
                TempData["ExportMessages"] = string.Join("\n", result.Messages.Take(20));
            }

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(await BuildViewModelAsync(model));
        }
    }

    private async Task<PmsExportViewModel> BuildViewModelAsync(PmsExportViewModel model)
    {
        model.RatePlans = await _db.RatePlans
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.RatePlanCode == "WALK-IN" || x.RatePlanName == "Walk-In Rates")
            .ThenBy(x => x.RatePlanName)
            .Select(x => new SelectListItem { Value = x.RatePlanId.ToString(), Text = x.RatePlanName })
            .ToListAsync();

        if (model.RatePlanId == 0 && model.RatePlans.Any())
        {
            model.RatePlanId = int.Parse(model.RatePlans.First().Value ?? "0");
        }

        var existingMappings = await _db.PmsRoomTypeMappings.ToDictionaryAsync(x => x.RoomTypeId, x => x.PmsRoomTypeCode);
        var rooms = await _db.RoomTypes.Where(x => x.IsActive).OrderBy(x => x.RoomName).ToListAsync();

        if (!model.RoomMappings.Any())
        {
            model.RoomMappings = rooms.Select(room => new PmsRoomTypeMappingInput
            {
                RoomTypeId = room.RoomTypeId,
                RoomCode = room.RoomCode,
                RoomName = room.RoomName,
                PmsRoomTypeCode = existingMappings.TryGetValue(room.RoomTypeId, out var pmsCode) ? pmsCode : string.Empty
            }).ToList();
        }
        else
        {
            foreach (var input in model.RoomMappings)
            {
                var room = rooms.FirstOrDefault(x => x.RoomTypeId == input.RoomTypeId);
                if (room != null)
                {
                    input.RoomCode = room.RoomCode;
                    input.RoomName = room.RoomName;
                }
            }
        }

        return model;
    }
}
