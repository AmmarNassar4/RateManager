using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RateManager.Net10.Data;
using RateManager.Net10.Services;
using RateManager.Net10.ViewModels;

namespace RateManager.Net10.Controllers;

public class RatesController : Controller
{
    private readonly AppDbContext _db;
    private readonly IRateCalculationService _calculationService;
    private readonly IExcelRateImportService _excelImportService;

    public RatesController(AppDbContext db, IRateCalculationService calculationService, IExcelRateImportService excelImportService)
    {
        _db = db;
        _calculationService = calculationService;
        _excelImportService = excelImportService;
    }

    [HttpGet]
    public async Task<IActionResult> Generate()
    {
        var model = await BuildGenerateViewModelAsync(new GenerateRatesViewModel
        {
            NumberOfDays = 30,
            StartDate = DateOnly.FromDateTime(DateTime.Today),
            Rooms = new List<RoomRateInputModel>
            {
                new RoomRateInputModel { GuestCount = 2, RoomCount = 1 }
            }
        });

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Generate(GenerateRatesViewModel model)
    {
        try
        {
            var batchId = await _calculationService.GenerateRatesAsync(model, User.Identity?.Name ?? Environment.UserName);
            return RedirectToAction(nameof(Calendar), new { batchId });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            model = await BuildGenerateViewModelAsync(model);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> ImportExcel()
    {
        var model = await BuildExcelImportViewModelAsync(new ExcelImportViewModel
        {
            StartDate = DateOnly.FromDateTime(DateTime.Today),
            NumberOfDays = 30,
            DefaultGuestCount = 2,
            DefaultRoomCount = 1
        });

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // Excel upload is bound through ExcelImportViewModel.ExcelFile.
    public async Task<IActionResult> ImportExcel(ExcelImportViewModel model)
    {
        try
        {
            var batchId = await _excelImportService.ImportAsync(model, User.Identity?.Name ?? Environment.UserName);
            return RedirectToAction(nameof(Calendar), new { batchId });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            model = await BuildExcelImportViewModelAsync(model);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Calendar(int batchId)
    {
        var batch = await _db.RateGenerationBatches
            .Include(x => x.RatePlan)
            .FirstOrDefaultAsync(x => x.RateGenerationBatchId == batchId);

        if (batch == null)
        {
            return NotFound();
        }

        var rates = await _db.DailyRoomRates
            .Include(x => x.RoomType)
            .Where(x => x.RateGenerationBatchId == batchId)
            .OrderBy(x => x.RateDate)
            .ThenBy(x => x.RoomType!.RoomName)
            .ThenBy(x => x.GuestCount)
            .ToListAsync();

        return View(new CalendarViewModel { Batch = batch, Rates = rates });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateManualRate([FromBody] ManualRateUpdateInput input)
    {
        try
        {
            await _calculationService.UpdateManualRateAsync(input, User.Identity?.Name ?? Environment.UserName);
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    private async Task<GenerateRatesViewModel> BuildGenerateViewModelAsync(GenerateRatesViewModel model)
    {
        model.RatePlans = await _db.RatePlans
            .Where(x => x.IsActive)
            .OrderBy(x => x.RatePlanName)
            .Select(x => new SelectListItem { Value = x.RatePlanId.ToString(), Text = x.RatePlanName })
            .ToListAsync();

        model.RoomTypes = await _db.RoomTypes
            .Where(x => x.IsActive)
            .OrderBy(x => x.RoomName)
            .Select(x => new SelectListItem { Value = x.RoomTypeId.ToString(), Text = x.RoomName })
            .ToListAsync();

        if (model.RatePlanId == 0 && model.RatePlans.Any())
        {
            model.RatePlanId = int.Parse(model.RatePlans.First().Value ?? "0");
        }

        return model;
    }

    private async Task<ExcelImportViewModel> BuildExcelImportViewModelAsync(ExcelImportViewModel model)
    {
        model.RatePlans = await _db.RatePlans
            .Where(x => x.IsActive)
            .OrderBy(x => x.RatePlanName)
            .Select(x => new SelectListItem { Value = x.RatePlanId.ToString(), Text = x.RatePlanName })
            .ToListAsync();

        if (model.RatePlanId == 0 && model.RatePlans.Any())
        {
            model.RatePlanId = int.Parse(model.RatePlans.First().Value ?? "0");
        }

        return model;
    }
}
