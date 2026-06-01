using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RateManager.Net10.Data;
using RateManager.Net10.Models;
using RateManager.Net10.ViewModels;

namespace RateManager.Net10.Services;

public class PmsExportService : IPmsExportService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;

    public PmsExportService(AppDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public async Task<PmsExportResultViewModel> ExportAsync(PmsExportViewModel model, string? userName)
    {
        var result = new PmsExportResultViewModel();
        var connectionString = _configuration.GetConnectionString("TargetPmsConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("TargetPmsConnection is missing from appsettings.json.");
        }

        var mealPrices = await _db.MealPriceSettings.OrderBy(x => x.MealPriceSettingId).FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("Pricing settings were not found.");

        await SaveMappingsAsync(model.RoomMappings);

        var activeMappings = model.RoomMappings
            .Where(x => x.RoomTypeId > 0 && !string.IsNullOrWhiteSpace(x.PmsRoomTypeCode))
            .ToList();

        if (!activeMappings.Any())
        {
            throw new InvalidOperationException("At least one PMS room type mapping is required.");
        }

        var exportRows = new List<PmsExportRow>();
        var plans = BuildPlanExports(model, mealPrices);

        foreach (var mapping in activeMappings)
        {
            for (var offset = 0; offset < model.NumberOfDays; offset++)
            {
                var date = model.StartDate.AddDays(offset);
                var singleRate = await GetLatestRateAsync(model.RatePlanId, mapping.RoomTypeId, date, 1);
                var doubleRate = await GetLatestRateAsync(model.RatePlanId, mapping.RoomTypeId, date, 2);
                var fallbackRate = singleRate ?? doubleRate ?? await GetLatestRateAsync(model.RatePlanId, mapping.RoomTypeId, date, null);

                if (fallbackRate == null)
                {
                    result.SkippedRows += plans.Count;
                    result.Messages.Add($"Skipped {date:yyyy-MM-dd} / {mapping.RoomName}: no rate was found.");
                    continue;
                }

                var finalSingleRate = singleRate ?? fallbackRate.Value;
                var finalDoubleRate = doubleRate ?? fallbackRate.Value;
                var dateString = date.ToString("yyyyMMdd");
                var dayCode = (int)date.ToDateTime(TimeOnly.MinValue).DayOfWeek;

                foreach (var plan in plans)
                {
                    exportRows.Add(new PmsExportRow
                    {
                        TrfTbl = 9,
                        PropertyCode = "RRK",
                        RoomTypeCode = mapping.PmsRoomTypeCode.Trim().ToUpperInvariant(),
                        FromDate = dateString,
                        ToDate = dateString,
                        PlanCode = plan.PlanCode,
                        DayCode = dayCode,
                        CurrencyCode = "SAR",
                        Description = "Rate Manager Export",
                        TableType = "R",
                        SingleRate = finalSingleRate,
                        DoubleRate = finalDoubleRate,
                        TripleRate = 0,
                        FourPersonRate = 0,
                        AdultRate = 0,
                        ChildRate = 0,
                        FoodCurrencyCode = "SAR",
                        FoodSingleRate = plan.FoodSingleRate,
                        FoodDoubleRate = plan.FoodDoubleRate,
                        FoodTripleRate = 0,
                        FoodFourPersonRate = 0,
                        FoodAdultRate = 0,
                        FoodChildRate = 0,
                        UserId = string.IsNullOrWhiteSpace(userName) ? "RMAPP" : userName.Length > 10 ? userName[..10] : userName
                    });
                }
            }
        }

        var orderedExportRows = exportRows
            .OrderBy(x => x.RoomTypeCode)
            .ThenBy(x => x.PlanCode)
            .ThenBy(x => x.DayCode)
            .ThenBy(x => x.FromDate)
            .ToList();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();

        try
        {
            foreach (var row in orderedExportRows)
            {
                var affected = await UpsertRowAsync(connection, transaction, row);
                if (affected == "INSERT")
                {
                    result.InsertedRows++;
                }
                else
                {
                    result.UpdatedRows++;
                }
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        result.Messages.Add($"Export completed. Inserted: {result.InsertedRows}, Updated: {result.UpdatedRows}, Skipped: {result.SkippedRows}.");
        return result;
    }

    private async Task SaveMappingsAsync(List<PmsRoomTypeMappingInput> mappings)
    {
        foreach (var input in mappings.Where(x => x.RoomTypeId > 0))
        {
            var existing = await _db.PmsRoomTypeMappings.FirstOrDefaultAsync(x => x.RoomTypeId == input.RoomTypeId);
            if (string.IsNullOrWhiteSpace(input.PmsRoomTypeCode))
            {
                continue;
            }

            if (existing == null)
            {
                _db.PmsRoomTypeMappings.Add(new PmsRoomTypeMapping
                {
                    RoomTypeId = input.RoomTypeId,
                    PmsRoomTypeCode = input.PmsRoomTypeCode.Trim().ToUpperInvariant()
                });
            }
            else
            {
                existing.PmsRoomTypeCode = input.PmsRoomTypeCode.Trim().ToUpperInvariant();
                existing.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync();
    }

    private static List<(string PlanCode, decimal FoodSingleRate, decimal FoodDoubleRate)> BuildPlanExports(PmsExportViewModel model, MealPriceSetting mealPrices)
    {
        var plans = new List<(string PlanCode, decimal FoodSingleRate, decimal FoodDoubleRate)>();

        if (model.ExportRo)
        {
            plans.Add(("RO", 0, 0));
        }

        if (model.ExportBb)
        {
            plans.Add(("BB", mealPrices.BreakfastPrice, mealPrices.BreakfastPrice * 2));
        }

        if (model.ExportHb)
        {
            plans.Add(("HB", mealPrices.LunchPrice, mealPrices.LunchPrice * 2));
        }

        if (model.ExportFb)
        {
            plans.Add(("FB", model.FbMealPrice, model.FbMealPrice * 2));
        }

        return plans;
    }

    private async Task<decimal?> GetLatestRateAsync(int ratePlanId, int roomTypeId, DateOnly date, int? guestCount)
    {
        var query = _db.DailyRoomRates
            .Where(x => x.RatePlanId == ratePlanId && x.RoomTypeId == roomTypeId && x.RateDate == date);

        if (guestCount.HasValue)
        {
            query = query.Where(x => x.GuestCount == guestCount.Value);
        }

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.DailyRoomRateId)
            .Select(x => (decimal?)x.FinalRate)
            .FirstOrDefaultAsync();
    }

    private static async Task<string> UpsertRowAsync(SqlConnection connection, SqlTransaction transaction, PmsExportRow row)
    {
        const string updateSql = @"
UPDATE pms.FMTTBTBL
SET
    CURCOD = @CURCOD,
    DESCRP = @DESCRP,
    TBLTYP = @TBLTYP,
    FTRSGL = @FTRSGL,
    FTRDBL = @FTRDBL,
    FTRTBL = @FTRTBL,
    FTRFOR = @FTRFOR,
    FTRADT = @FTRADT,
    FTRCHD = @FTRCHD,
    FCURCD = @FCURCD,
    FFBSGL = @FFBSGL,
    FFBDBL = @FFBDBL,
    FFBTBL = @FFBTBL,
    FFBFOR = @FFBFOR,
    FFBADT = @FFBADT,
    FFBCHD = @FFBCHD,
    FTTXST = '1',
    FFTXST = '1',
    FTETAX = '1',
    FPETAX = '1',
    USERID = @USERID,
    LSTDAT = @LSTDAT,
    LSTTIM = @LSTTIM,
    FUTU01 = '0',
    FUTU02 = '0',
    FUTU03 = '0',
    FUTU04 = '',
    FUTU05 = '',
    FUTU06 = ' '
WHERE TRFTBL = @TRFTBL
  AND PRPCOD = @PRPCOD
  AND ROMTYP = @ROMTYP
  AND FRMDAT = @FRMDAT
  AND TOODAT = @TOODAT
  AND PLNCOD = @PLNCOD
  AND DAYCOD = @DAYCOD;";

        await using var updateCommand = new SqlCommand(updateSql, connection, transaction);
        AddParameters(updateCommand, row);
        var updated = await updateCommand.ExecuteNonQueryAsync();
        if (updated > 0)
        {
            return "UPDATE";
        }

        const string insertSql = @"
INSERT INTO pms.FMTTBTBL
(
    TRFTBL, PRPCOD, ROMTYP, FRMDAT, TOODAT, PLNCOD, DAYCOD, CURCOD, DESCRP, TBLTYP,
    FTRSGL, FTRDBL, FTRTBL, FTRFOR, FTRADT, FTRCHD, FCURCD,
    FFBSGL, FFBDBL, FFBTBL, FFBFOR, FFBADT, FFBCHD,
    FTTXST, FFTXST, FTETAX, FPETAX, USERID, LSTDAT, LSTTIM,
    FUTU01, FUTU02, FUTU03, FUTU04, FUTU05, FUTU06
)
VALUES
(
    @TRFTBL, @PRPCOD, @ROMTYP, @FRMDAT, @TOODAT, @PLNCOD, @DAYCOD, @CURCOD, @DESCRP, @TBLTYP,
    @FTRSGL, @FTRDBL, @FTRTBL, @FTRFOR, @FTRADT, @FTRCHD, @FCURCD,
    @FFBSGL, @FFBDBL, @FFBTBL, @FFBFOR, @FFBADT, @FFBCHD,
    '1', '1', '1', '1', @USERID, @LSTDAT, @LSTTIM,
    '0', '0', '0', '', '', ' '
);";

        await using var insertCommand = new SqlCommand(insertSql, connection, transaction);
        AddParameters(insertCommand, row);
        await insertCommand.ExecuteNonQueryAsync();
        return "INSERT";
    }

    private static void AddParameters(SqlCommand command, PmsExportRow row)
    {
        var now = DateTime.Now;
        command.Parameters.AddWithValue("@TRFTBL", row.TrfTbl);
        command.Parameters.AddWithValue("@PRPCOD", row.PropertyCode);
        command.Parameters.AddWithValue("@ROMTYP", row.RoomTypeCode);
        command.Parameters.AddWithValue("@FRMDAT", row.FromDate);
        command.Parameters.AddWithValue("@TOODAT", row.ToDate);
        command.Parameters.AddWithValue("@PLNCOD", row.PlanCode);
        command.Parameters.AddWithValue("@DAYCOD", row.DayCode);
        command.Parameters.AddWithValue("@CURCOD", row.CurrencyCode);
        command.Parameters.AddWithValue("@DESCRP", row.Description);
        command.Parameters.AddWithValue("@TBLTYP", row.TableType);
        command.Parameters.AddWithValue("@FTRSGL", row.SingleRate);
        command.Parameters.AddWithValue("@FTRDBL", row.DoubleRate);
        command.Parameters.AddWithValue("@FTRTBL", row.TripleRate);
        command.Parameters.AddWithValue("@FTRFOR", row.FourPersonRate);
        command.Parameters.AddWithValue("@FTRADT", row.AdultRate);
        command.Parameters.AddWithValue("@FTRCHD", row.ChildRate);
        command.Parameters.AddWithValue("@FCURCD", row.FoodCurrencyCode);
        command.Parameters.AddWithValue("@FFBSGL", row.FoodSingleRate);
        command.Parameters.AddWithValue("@FFBDBL", row.FoodDoubleRate);
        command.Parameters.AddWithValue("@FFBTBL", row.FoodTripleRate);
        command.Parameters.AddWithValue("@FFBFOR", row.FoodFourPersonRate);
        command.Parameters.AddWithValue("@FFBADT", row.FoodAdultRate);
        command.Parameters.AddWithValue("@FFBCHD", row.FoodChildRate);
        command.Parameters.AddWithValue("@USERID", row.UserId);
        command.Parameters.AddWithValue("@LSTDAT", row.ToDate);
        command.Parameters.AddWithValue("@LSTTIM", now.ToString("HH.mm"));
    }
}