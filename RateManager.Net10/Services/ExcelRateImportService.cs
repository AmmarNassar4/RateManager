using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RateManager.Net10.Data;
using RateManager.Net10.Models;
using RateManager.Net10.ViewModels;

namespace RateManager.Net10.Services;

public class ExcelRateImportService : IExcelRateImportService
{
    private readonly AppDbContext _db;
    private readonly IRateAuditService _audit;

    public ExcelRateImportService(AppDbContext db, IRateAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<int> ImportAsync(ExcelImportViewModel model, string? userName)
    {
        var regularPlan = await _db.RatePlans.FirstOrDefaultAsync(x => x.RatePlanId == model.RatePlanId && x.IsActive);
        if (regularPlan == null)
        {
            throw new InvalidOperationException("Regular rate plan was not found.");
        }

        RatePlan? discountPlan = null;
        if (model.DiscountRatePlanId.HasValue)
        {
            discountPlan = await _db.RatePlans.FirstOrDefaultAsync(x => x.RatePlanId == model.DiscountRatePlanId.Value && x.IsActive);
            if (discountPlan == null)
            {
                throw new InvalidOperationException("Discount rate plan was not found.");
            }
        }

        var (excelPath, sourceFileName, tempFilePath) = await ResolveExcelFileAsync(model);

        try
        {
            var startDate = model.StartDate;
            var endDate = model.StartDate.AddDays(model.NumberOfDays - 1);
            var importedRows = ReadRatesFromExcel(excelPath);

            var regularRows = importedRows
                .Where(x => x.RateKind == ExcelRateKind.Regular && x.RateDate >= startDate && x.RateDate <= endDate)
                .ToList();

            var discountRows = importedRows
                .Where(x => x.RateKind == ExcelRateKind.Discount && x.RateDate >= startDate && x.RateDate <= endDate)
                .ToList();

            if (!regularRows.Any())
            {
                throw new InvalidOperationException("No regular / without-discount rates were found in the Excel file.");
            }

            var firstBatchId = await SaveRowsAsync(
                regularPlan,
                regularRows,
                model,
                sourceFileName,
                "Excel import - regular rates",
                userName);

            if (discountPlan != null)
            {
                if (!discountRows.Any())
                {
                    throw new InvalidOperationException("No discounted rates were found in the Excel file. Discounted rates must be imported from Excel cells, not calculated from a fixed percentage.");
                }

                await SaveRowsAsync(
                    discountPlan,
                    discountRows,
                    model,
                    sourceFileName,
                    "Excel import - discounted rates",
                    userName);
            }

            return firstBatchId;
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(tempFilePath) && File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                }
                catch
                {
                    // Ignore temp cleanup errors.
                }
            }
        }
    }

    private async Task<int> SaveRowsAsync(
        RatePlan ratePlan,
        List<ExcelRateRow> rows,
        ExcelImportViewModel model,
        string sourceFileName,
        string notesPrefix,
        string? userName)
    {
        var startDate = model.StartDate;
        var endDate = model.StartDate.AddDays(model.NumberOfDays - 1);

        var batch = new RateGenerationBatch
        {
            RatePlanId = ratePlan.RatePlanId,
            SourceType = RateSourceType.ExcelImport,
            StartDate = startDate,
            EndDate = endDate,
            NumberOfDays = model.NumberOfDays,
            SourceFilePath = sourceFileName,
            Notes = string.IsNullOrWhiteSpace(model.Notes) ? notesPrefix : $"{notesPrefix} - {model.Notes}",
            CreatedBy = userName
        };

        _db.RateGenerationBatches.Add(batch);
        await _db.SaveChangesAsync();

        var configuredWeekendDays = await _db.WeekendDaySettings
            .Where(x => x.Enabled)
            .Select(x => x.Weekday)
            .ToListAsync();

        foreach (var row in rows)
        {
            var roomType = await GetOrCreateRoomTypeAsync(row.RoomTypeName);
            var dateTime = row.RateDate.ToDateTime(TimeOnly.MinValue);
            var rate = Math.Round(row.Rate, 3, MidpointRounding.AwayFromZero);

            _db.DailyRoomRates.Add(new DailyRoomRate
            {
                RateGenerationBatchId = batch.RateGenerationBatchId,
                RatePlanId = ratePlan.RatePlanId,
                RoomTypeId = roomType.RoomTypeId,
                RateDate = row.RateDate,
                DayName = dateTime.DayOfWeek.ToString(),
                IsWeekend = configuredWeekendDays.Contains(dateTime.DayOfWeek),
                GuestCount = row.GuestCount ?? model.DefaultGuestCount,
                RoomCount = row.RoomCount ?? model.DefaultRoomCount,
                BaseRate = rate,
                TotalAdjustmentPercent = 0,
                CalculatedRate = rate,
                FinalRate = rate,
                CalculationNote = $"Imported {row.RateKind} from {row.SheetName} / {row.SectionName}",
                CreatedBy = userName
            });
        }

        await _db.SaveChangesAsync();
        await _audit.WriteAsync("RateGenerationBatch", batch.RateGenerationBatchId, "Import", "SourceFilePath", null, sourceFileName, userName);
        return batch.RateGenerationBatchId;
    }

    private static async Task<(string ExcelPath, string SourceFileName, string? TempFilePath)> ResolveExcelFileAsync(ExcelImportViewModel model)
    {
        if (model.ExcelFile != null && model.ExcelFile.Length > 0)
        {
            var tempPath = await SaveUploadedExcelFileAsync(model.ExcelFile);
            return (tempPath, model.ExcelFile.FileName, tempPath);
        }

        if (!string.IsNullOrWhiteSpace(model.ExcelFilePath))
        {
            if (!File.Exists(model.ExcelFilePath))
            {
                throw new FileNotFoundException("Excel file was not found.", model.ExcelFilePath);
            }

            return (model.ExcelFilePath, model.ExcelFilePath, null);
        }

        throw new InvalidOperationException("Please choose an Excel file first.");
    }

    private static async Task<string> SaveUploadedExcelFileAsync(IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".xlsx",
            ".xlsm",
            ".xltx",
            ".xltm"
        };

        if (!allowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("Unsupported Excel file type. Please upload .xlsx or .xlsm file.");
        }

        var uploadsFolder = Path.Combine(Path.GetTempPath(), "RateManagerExcelUploads");
        Directory.CreateDirectory(uploadsFolder);

        var safeFileName = $"{Guid.NewGuid():N}{extension}";
        var tempPath = Path.Combine(uploadsFolder, safeFileName);

        await using var stream = File.Create(tempPath);
        await file.CopyToAsync(stream);

        return tempPath;
    }

    private async Task<RoomType> GetOrCreateRoomTypeAsync(string roomName)
    {
        var cleanName = roomName.Trim();
        var code = MakeRoomCode(cleanName);

        var room = await _db.RoomTypes.FirstOrDefaultAsync(x => x.RoomCode == code || x.RoomName == cleanName);
        if (room != null)
        {
            return room;
        }

        room = new RoomType
        {
            RoomCode = code,
            RoomName = cleanName,
            IsActive = true
        };

        _db.RoomTypes.Add(room);
        await _db.SaveChangesAsync();
        return room;
    }

    private static List<ExcelRateRow> ReadRatesFromExcel(string path)
    {
        using var workbook = new XLWorkbook(path);
        var rows = new List<ExcelRateRow>();

        foreach (var worksheet in workbook.Worksheets)
        {
            var range = worksheet.RangeUsed();
            if (range == null)
            {
                continue;
            }

            rows.AddRange(ReadNormalizedTable(worksheet));
            rows.AddRange(ReadMatrixSections(worksheet));
        }

        return rows
            .GroupBy(x => new
            {
                x.RateKind,
                x.RateDate,
                Room = x.RoomTypeName.Trim().ToUpperInvariant(),
                x.GuestCount,
                x.RoomCount
            })
            .Select(g => g.First())
            .ToList();
    }

    private static IEnumerable<ExcelRateRow> ReadNormalizedTable(IXLWorksheet worksheet)
    {
        var range = worksheet.RangeUsed();
        if (range == null)
        {
            yield break;
        }

        foreach (var row in range.RowsUsed().Take(30))
        {
            var headers = row.CellsUsed()
                .Select(c => new { Column = c.Address.ColumnNumber, Header = NormalizeHeader(c.GetString()) })
                .Where(x => !string.IsNullOrWhiteSpace(x.Header))
                .ToList();

            var dateCol = headers.FirstOrDefault(x => x.Header is "date" or "ratedate" or "day")?.Column;
            var roomCol = headers.FirstOrDefault(x => x.Header is "room" or "roomtype" or "roomname" or "typeofroom")?.Column;
            var rateCol = headers.FirstOrDefault(x => x.Header is "rate" or "price" or "finalrate" or "calculatedrate" or "regularrate")?.Column;
            var discountRateCol = headers.FirstOrDefault(x => x.Header is "discountrate" or "discountedrate" or "rateafterdiscount")?.Column;
            var guestsCol = headers.FirstOrDefault(x => x.Header is "guests" or "guestcount" or "pax" or "occupancy")?.Column;
            var roomsCol = headers.FirstOrDefault(x => x.Header is "rooms" or "roomcount" or "numberofrooms")?.Column;

            if (!dateCol.HasValue || !roomCol.HasValue || !rateCol.HasValue)
            {
                continue;
            }

            var firstDataRow = row.RowNumber() + 1;
            var lastRow = range.LastRow().RowNumber();

            for (var r = firstDataRow; r <= lastRow; r++)
            {
                var roomName = worksheet.Cell(r, roomCol.Value).GetString().Trim();
                if (string.IsNullOrWhiteSpace(roomName) || !TryGetDate(worksheet.Cell(r, dateCol.Value), out var rateDate))
                {
                    continue;
                }

                int? guests = guestsCol.HasValue && TryGetInt(worksheet.Cell(r, guestsCol.Value), out var parsedGuests) ? parsedGuests : null;
                int? roomCount = roomsCol.HasValue && TryGetInt(worksheet.Cell(r, roomsCol.Value), out var parsedRooms) ? parsedRooms : null;

                if (TryGetDecimal(worksheet.Cell(r, rateCol.Value), out var regularRate))
                {
                    yield return new ExcelRateRow(worksheet.Name, "Normalized", ExcelRateKind.Regular, rateDate, roomName, regularRate, guests, roomCount);
                }

                if (discountRateCol.HasValue && TryGetDecimal(worksheet.Cell(r, discountRateCol.Value), out var discountRate))
                {
                    yield return new ExcelRateRow(worksheet.Name, "Normalized", ExcelRateKind.Discount, rateDate, roomName, discountRate, guests, roomCount);
                }
            }
        }
    }

    private static IEnumerable<ExcelRateRow> ReadMatrixSections(IXLWorksheet worksheet)
    {
        var range = worksheet.RangeUsed();
        if (range == null)
        {
            yield break;
        }

        var firstRow = range.FirstRow().RowNumber();
        var lastRow = range.LastRow().RowNumber();
        var firstCol = range.FirstColumn().ColumnNumber();
        var lastCol = range.LastColumn().ColumnNumber();

        for (var headerRow = firstRow; headerRow <= lastRow; headerRow++)
        {
            var dateColumns = new List<(int Column, DateOnly Date)>();
            for (var col = firstCol; col <= lastCol; col++)
            {
                if (TryGetDate(worksheet.Cell(headerRow, col), out var date))
                {
                    dateColumns.Add((col, date));
                }
            }

            if (dateColumns.Count < 2)
            {
                continue;
            }

            var sectionName = FindSectionNameAbove(worksheet, headerRow, firstCol, lastCol);
            var rateKind = DetectRateKind(sectionName);
            var roomNameColumn = Math.Max(firstCol, dateColumns.Min(x => x.Column) - 1);
            var nextHeaderRow = FindNextDateHeaderRow(worksheet, headerRow + 1, lastRow, firstCol, lastCol);
            var sectionLastRow = nextHeaderRow.HasValue ? nextHeaderRow.Value - 1 : lastRow;

            for (var r = headerRow + 1; r <= sectionLastRow; r++)
            {
                var roomName = worksheet.Cell(r, roomNameColumn).GetString().Trim();
                if (string.IsNullOrWhiteSpace(roomName) || LooksLikeHeader(roomName))
                {
                    continue;
                }

                foreach (var dateColumn in dateColumns)
                {
                    if (!TryGetDecimal(worksheet.Cell(r, dateColumn.Column), out var rate))
                    {
                        continue;
                    }

                    yield return new ExcelRateRow(worksheet.Name, sectionName, rateKind, dateColumn.Date, roomName, rate, null, null);
                }
            }
        }
    }

    private static int? FindNextDateHeaderRow(IXLWorksheet worksheet, int startRow, int lastRow, int firstCol, int lastCol)
    {
        for (var row = startRow; row <= lastRow; row++)
        {
            var count = 0;
            for (var col = firstCol; col <= lastCol; col++)
            {
                if (TryGetDate(worksheet.Cell(row, col), out _))
                {
                    count++;
                }
            }

            if (count >= 2)
            {
                return row;
            }
        }

        return null;
    }

    private static string FindSectionNameAbove(IXLWorksheet worksheet, int headerRow, int firstCol, int lastCol)
    {
        for (var row = headerRow - 1; row >= Math.Max(1, headerRow - 8); row--)
        {
            var text = string.Join(" ", Enumerable.Range(firstCol, lastCol - firstCol + 1)
                .Select(col => worksheet.Cell(row, col).GetString().Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value)));

            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        return worksheet.Name;
    }

    private static ExcelRateKind DetectRateKind(string sectionName)
    {
        var normalized = NormalizeHeader(sectionName);
        if (normalized.Contains("discount") || normalized.Contains("family") || normalized.Contains("friends"))
        {
            return ExcelRateKind.Discount;
        }

        return ExcelRateKind.Regular;
    }

    private static bool LooksLikeHeader(string value)
    {
        var normalized = NormalizeHeader(value);
        return normalized.Contains("total") || normalized.Contains("date") || normalized.Contains("roomtype") || normalized.Contains("discount");
    }

    private static string NormalizeHeader(string value)
    {
        return new string(value
            .Trim()
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
    }

    private static bool TryGetDate(IXLCell cell, out DateOnly date)
    {
        if (cell.DataType == XLDataType.DateTime && cell.TryGetValue<DateTime>(out var dt))
        {
            date = DateOnly.FromDateTime(dt);
            return true;
        }

        var text = cell.GetString().Trim();
        if (DateTime.TryParse(text, out dt))
        {
            date = DateOnly.FromDateTime(dt);
            return true;
        }

        date = default;
        return false;
    }

    private static bool TryGetDecimal(IXLCell cell, out decimal value)
    {
        if (cell.TryGetValue<decimal>(out value))
        {
            return true;
        }

        var text = cell.GetString().Trim().Replace(",", string.Empty);
        return decimal.TryParse(text, out value);
    }

    private static bool TryGetInt(IXLCell cell, out int value)
    {
        if (cell.TryGetValue<int>(out value))
        {
            return true;
        }

        var text = cell.GetString().Trim();
        return int.TryParse(text, out value);
    }

    private static string MakeRoomCode(string roomName)
    {
        var code = new string(roomName
            .Trim()
            .ToUpperInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray());

        while (code.Contains("--", StringComparison.Ordinal))
        {
            code = code.Replace("--", "-", StringComparison.Ordinal);
        }

        return code.Trim('-');
    }

    private enum ExcelRateKind
    {
        Regular,
        Discount
    }

    private sealed record ExcelRateRow(
        string SheetName,
        string SectionName,
        ExcelRateKind RateKind,
        DateOnly RateDate,
        string RoomTypeName,
        decimal Rate,
        int? GuestCount,
        int? RoomCount);
}
