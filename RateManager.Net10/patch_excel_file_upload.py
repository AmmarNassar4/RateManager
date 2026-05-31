#!/usr/bin/env python3
# -*- coding: utf-8 -*-

# patch_excel_file_upload.py
#
# Run this script from the ASP.NET Core project folder that contains RateManager.Net10.csproj.
#
# What this patch does:
# 1) Changes the Excel import screen from "file path textbox" to a real file picker button.
# 2) Adds IFormFile ExcelFile to ExcelImportViewModel.
# 3) Updates ExcelRateImportService to accept uploaded Excel files.
# 4) Keeps the old ExcelFilePath option as a fallback.
# 5) Creates .bak backups before editing files.

from __future__ import annotations

import re
from pathlib import Path


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


def write_text(path: Path, text: str) -> None:
    path.write_text(text, encoding="utf-8")


def backup(path: Path) -> None:
    if not path.exists():
        return

    bak = path.with_suffix(path.suffix + ".bak")
    if not bak.exists():
        bak.write_text(read_text(path), encoding="utf-8")


def find_project_root(start: Path) -> Path | None:
    current = start.resolve()

    for folder in [current, *current.parents]:
        if list(folder.glob("*.csproj")):
            return folder

    for candidate in current.glob("*/*.csproj"):
        return candidate.parent

    for candidate in current.glob("*/*/*.csproj"):
        return candidate.parent

    return None


def extract_namespace(path: Path, default_namespace: str) -> str:
    if not path.exists():
        return default_namespace

    text = read_text(path)
    match = re.search(r"^\s*namespace\s+([A-Za-z0-9_.]+)\s*;", text, flags=re.MULTILINE)
    if match:
        return match.group(1)

    return default_namespace


def ensure_package_reference(csproj_path: Path) -> bool:
    text = read_text(csproj_path)

    if 'Include="ClosedXML"' in text:
        return False

    package_line = '    <PackageReference Include="ClosedXML" Version="0.104.2" />\n'

    if "</ItemGroup>" in text:
        text = text.replace("</ItemGroup>", package_line + "  </ItemGroup>", 1)
    else:
        text = text.replace("</Project>", "  <ItemGroup>\n" + package_line + "  </ItemGroup>\n</Project>")

    backup(csproj_path)
    write_text(csproj_path, text)
    return True


def patch_view_model(project_root: Path, namespace: str) -> bool:
    path = project_root / "ViewModels" / "ExcelImportViewModel.cs"
    path.parent.mkdir(parents=True, exist_ok=True)

    content = f"""using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace {namespace};

public class ExcelImportViewModel
{{
    public int RatePlanId {{ get; set; }}

    // Main import option: select the Excel file from the browser.
    public IFormFile? ExcelFile {{ get; set; }}

    // Optional fallback: server-side file path.
    // Kept for compatibility with the previous version.
    public string ExcelFilePath {{ get; set; }} = string.Empty;

    public DateOnly StartDate {{ get; set; }} = DateOnly.FromDateTime(DateTime.Today);
    public int NumberOfDays {{ get; set; }} = 30;
    public int DefaultGuestCount {{ get; set; }} = 2;
    public int DefaultRoomCount {{ get; set; }} = 1;
    public string? Notes {{ get; set; }}

    public List<SelectListItem> RatePlans {{ get; set; }} = new();
}}
"""

    old = read_text(path) if path.exists() else ""
    if old == content:
        return False

    backup(path)
    write_text(path, content)
    return True


def patch_import_view(project_root: Path) -> bool:
    path = project_root / "Views" / "Rates" / "ImportExcel.cshtml"
    path.parent.mkdir(parents=True, exist_ok=True)

    content = """@model ExcelImportViewModel
@{
    ViewData["Title"] = "استيراد Excel";
}

<section class="page-header">
    <h1>استيراد الأسعار من Excel</h1>
    <p>اختر ملف Excel من جهازك، وسيتم أخذ الأسعار منه بدل الحساب اليدوي.</p>
</section>

<form asp-action="ImportExcel" method="post" enctype="multipart/form-data" class="card form-card">
    <div asp-validation-summary="ModelOnly" class="validation"></div>

    <div class="form-grid">
        <label>
            خطة السعر
            <select asp-for="RatePlanId" asp-items="Model.RatePlans"></select>
        </label>

        <label>
            تاريخ البداية
            <input asp-for="StartDate" type="date" />
        </label>

        <label>
            عدد الأيام
            <input asp-for="NumberOfDays" type="number" min="1" max="365" />
        </label>

        <label>
            عدد النزلاء الافتراضي
            <input asp-for="DefaultGuestCount" type="number" min="1" />
        </label>

        <label>
            عدد الغرف الافتراضي
            <input asp-for="DefaultRoomCount" type="number" min="1" />
        </label>
    </div>

    <label>
        اختر ملف Excel
        <input asp-for="ExcelFile" type="file" accept=".xlsx,.xlsm,.xltx,.xltm" />
        <span asp-validation-for="ExcelFile" class="validation"></span>
    </label>

    <details class="hint">
        <summary>اختياري: استخدام مسار ملف على السيرفر بدل اختيار ملف</summary>
        <label>
            مسار ملف Excel على السيرفر
            <input asp-for="ExcelFilePath" placeholder="C:/Rates/Weekly Walk-In Rates.xlsx" />
        </label>
    </details>

    <label>
        ملاحظات
        <input asp-for="Notes" />
    </label>

    <div class="hint">
        يدعم الاستيراد جدولًا عاديًا فيه أعمدة Date / RoomType / Rate، أو Matrix حيث التواريخ في الأعمدة وأنواع الغرف في الصفوف.
        يفضّل استخدام ملفات .xlsx أو .xlsm.
    </div>

    <div class="actions-row">
        <button type="submit" class="button primary">Import</button>
        <a asp-controller="Home" asp-action="Index" class="button muted">إلغاء</a>
    </div>
</form>
"""

    old = read_text(path) if path.exists() else ""
    if old == content:
        return False

    backup(path)
    write_text(path, content)
    return True


def patch_excel_service(project_root: Path, view_model_namespace: str) -> bool:
    root_namespace = view_model_namespace.replace(".ViewModels", "")
    path = project_root / "Services" / "ExcelRateImportService.cs"
    path.parent.mkdir(parents=True, exist_ok=True)

    content = f"""using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using {root_namespace}.Data;
using {root_namespace}.Models;
using {root_namespace}.ViewModels;

namespace {root_namespace}.Services;

public class ExcelRateImportService : IExcelRateImportService
{{
    private readonly AppDbContext _db;
    private readonly IRateAuditService _audit;

    public ExcelRateImportService(AppDbContext db, IRateAuditService audit)
    {{
        _db = db;
        _audit = audit;
    }}

    public async Task<int> ImportAsync(ExcelImportViewModel model, string? userName)
    {{
        var ratePlan = await _db.RatePlans.FirstOrDefaultAsync(x => x.RatePlanId == model.RatePlanId && x.IsActive);
        if (ratePlan == null)
        {{
            throw new InvalidOperationException("Rate plan was not found.");
        }}

        string excelPath;
        string sourceFileName;
        string? tempFilePath = null;

        if (model.ExcelFile != null && model.ExcelFile.Length > 0)
        {{
            tempFilePath = await SaveUploadedExcelFileAsync(model.ExcelFile);
            excelPath = tempFilePath;
            sourceFileName = model.ExcelFile.FileName;
        }}
        else if (!string.IsNullOrWhiteSpace(model.ExcelFilePath))
        {{
            if (!File.Exists(model.ExcelFilePath))
            {{
                throw new FileNotFoundException("Excel file was not found.", model.ExcelFilePath);
            }}

            excelPath = model.ExcelFilePath;
            sourceFileName = model.ExcelFilePath;
        }}
        else
        {{
            throw new InvalidOperationException("Please choose an Excel file first.");
        }}

        try
        {{
            var startDate = model.StartDate;
            var endDate = model.StartDate.AddDays(model.NumberOfDays - 1);

            var batch = new RateGenerationBatch
            {{
                RatePlanId = ratePlan.RatePlanId,
                SourceType = RateSourceType.ExcelImport,
                StartDate = startDate,
                EndDate = endDate,
                NumberOfDays = model.NumberOfDays,
                SourceFilePath = sourceFileName,
                Notes = model.Notes,
                CreatedBy = userName
            }};

            _db.RateGenerationBatches.Add(batch);
            await _db.SaveChangesAsync();

            var importedRows = ReadRatesFromExcel(excelPath, model.StartDate, model.NumberOfDays);
            var filteredRows = importedRows
                .Where(x => x.RateDate >= startDate && x.RateDate <= endDate)
                .ToList();

            if (!filteredRows.Any())
            {{
                throw new InvalidOperationException("No rate rows were found in the Excel file. Supported formats are a normalized table with Date, RoomType, Rate columns, or a matrix where dates are columns and room types are rows.");
            }}

            var weekendDays = await _db.WeekendDaySettings
                .Where(x => x.IsWeekend)
                .Select(x => x.Weekday)
                .ToListAsync();

            foreach (var row in filteredRows)
            {{
                var roomType = await GetOrCreateRoomTypeAsync(row.RoomTypeName);
                var dateTime = row.RateDate.ToDateTime(TimeOnly.MinValue);
                var rate = Math.Round(row.Rate, 3, MidpointRounding.AwayFromZero);

                _db.DailyRoomRates.Add(new DailyRoomRate
                {{
                    RateGenerationBatchId = batch.RateGenerationBatchId,
                    RatePlanId = ratePlan.RatePlanId,
                    RoomTypeId = roomType.RoomTypeId,
                    RateDate = row.RateDate,
                    DayName = dateTime.DayOfWeek.ToString(),
                    IsWeekend = weekendDays.Contains(dateTime.DayOfWeek),
                    GuestCount = row.GuestCount ?? model.DefaultGuestCount,
                    RoomCount = row.RoomCount ?? model.DefaultRoomCount,
                    BaseRate = rate,
                    TotalAdjustmentPercent = 0,
                    CalculatedRate = rate,
                    FinalRate = rate,
                    CalculationNote = $"Imported from Excel sheet {{row.SheetName}}",
                    CreatedBy = userName
                }});
            }}

            await _db.SaveChangesAsync();
            await _audit.WriteAsync("RateGenerationBatch", batch.RateGenerationBatchId, "Import", "SourceFilePath", null, sourceFileName, userName);

            return batch.RateGenerationBatchId;
        }}
        finally
        {{
            if (!string.IsNullOrWhiteSpace(tempFilePath) && File.Exists(tempFilePath))
            {{
                try
                {{
                    File.Delete(tempFilePath);
                }}
                catch
                {{
                    // Ignore temp cleanup errors.
                }}
            }}
        }}
    }}

    private static async Task<string> SaveUploadedExcelFileAsync(IFormFile file)
    {{
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {{
            ".xlsx",
            ".xlsm",
            ".xltx",
            ".xltm"
        }};

        if (!allowedExtensions.Contains(extension))
        {{
            throw new InvalidOperationException("Unsupported Excel file type. Please upload .xlsx or .xlsm file.");
        }}

        var uploadsFolder = Path.Combine(Path.GetTempPath(), "RateManagerExcelUploads");
        Directory.CreateDirectory(uploadsFolder);

        var safeFileName = $"{{Guid.NewGuid():N}}{{extension}}";
        var tempPath = Path.Combine(uploadsFolder, safeFileName);

        await using var stream = File.Create(tempPath);
        await file.CopyToAsync(stream);

        return tempPath;
    }}

    private async Task<RoomType> GetOrCreateRoomTypeAsync(string roomName)
    {{
        var cleanName = roomName.Trim();
        var code = MakeRoomCode(cleanName);

        var room = await _db.RoomTypes.FirstOrDefaultAsync(x => x.RoomCode == code || x.RoomName == cleanName);
        if (room != null)
        {{
            return room;
        }}

        room = new RoomType
        {{
            RoomCode = code,
            RoomName = cleanName,
            IsActive = true
        }};

        _db.RoomTypes.Add(room);
        await _db.SaveChangesAsync();
        return room;
    }}

    private static List<ExcelRateRow> ReadRatesFromExcel(string path, DateOnly fallbackStartDate, int numberOfDays)
    {{
        using var workbook = new XLWorkbook(path);
        var rows = new List<ExcelRateRow>();

        foreach (var worksheet in workbook.Worksheets)
        {{
            var range = worksheet.RangeUsed();
            if (range == null)
            {{
                continue;
            }}

            rows.AddRange(ReadNormalizedTable(worksheet));
            rows.AddRange(ReadMatrixTable(worksheet, fallbackStartDate, numberOfDays));
        }}

        return rows
            .GroupBy(x => new {{ x.RateDate, Room = x.RoomTypeName.Trim().ToUpperInvariant(), x.GuestCount, x.RoomCount }})
            .Select(g => g.First())
            .ToList();
    }}

    private static IEnumerable<ExcelRateRow> ReadNormalizedTable(IXLWorksheet worksheet)
    {{
        var range = worksheet.RangeUsed();
        if (range == null)
        {{
            yield break;
        }}

        foreach (var row in range.RowsUsed().Take(30))
        {{
            var headers = row.CellsUsed()
                .Select(c => new {{ Column = c.Address.ColumnNumber, Header = NormalizeHeader(c.GetString()) }})
                .Where(x => !string.IsNullOrWhiteSpace(x.Header))
                .ToList();

            var dateCol = headers.FirstOrDefault(x => x.Header is "date" or "ratedate" or "day")?.Column;
            var roomCol = headers.FirstOrDefault(x => x.Header is "room" or "roomtype" or "roomname" or "typeofroom")?.Column;
            var rateCol = headers.FirstOrDefault(x => x.Header is "rate" or "price" or "finalrate" or "calculatedrate")?.Column;
            var guestsCol = headers.FirstOrDefault(x => x.Header is "guests" or "guestcount" or "pax" or "occupancy")?.Column;
            var roomsCol = headers.FirstOrDefault(x => x.Header is "rooms" or "roomcount" or "numberofrooms")?.Column;

            if (!dateCol.HasValue || !roomCol.HasValue || !rateCol.HasValue)
            {{
                continue;
            }}

            var firstDataRow = row.RowNumber() + 1;
            var lastRow = range.LastRow().RowNumber();

            for (var r = firstDataRow; r <= lastRow; r++)
            {{
                var rateCell = worksheet.Cell(r, rateCol.Value);
                var roomName = worksheet.Cell(r, roomCol.Value).GetString().Trim();

                if (string.IsNullOrWhiteSpace(roomName) || !TryGetDate(worksheet.Cell(r, dateCol.Value), out var rateDate) || !TryGetDecimal(rateCell, out var rate))
                {{
                    continue;
                }}

                int? guests = null;
                int? roomCount = null;

                if (guestsCol.HasValue && TryGetInt(worksheet.Cell(r, guestsCol.Value), out var parsedGuests))
                {{
                    guests = parsedGuests;
                }}

                if (roomsCol.HasValue && TryGetInt(worksheet.Cell(r, roomsCol.Value), out var parsedRooms))
                {{
                    roomCount = parsedRooms;
                }}

                yield return new ExcelRateRow(worksheet.Name, rateDate, roomName, rate, guests, roomCount);
            }}
        }}
    }}

    private static IEnumerable<ExcelRateRow> ReadMatrixTable(IXLWorksheet worksheet, DateOnly fallbackStartDate, int numberOfDays)
    {{
        var range = worksheet.RangeUsed();
        if (range == null)
        {{
            yield break;
        }}

        var firstRow = range.FirstRow().RowNumber();
        var lastRow = range.LastRow().RowNumber();
        var firstCol = range.FirstColumn().ColumnNumber();
        var lastCol = range.LastColumn().ColumnNumber();

        for (var headerRow = firstRow; headerRow <= Math.Min(firstRow + 25, lastRow); headerRow++)
        {{
            var dateColumns = new List<(int Column, DateOnly Date)>();

            for (var col = firstCol; col <= lastCol; col++)
            {{
                if (TryGetDate(worksheet.Cell(headerRow, col), out var date))
                {{
                    dateColumns.Add((col, date));
                }}
            }}

            if (dateColumns.Count < 2)
            {{
                continue;
            }}

            var roomNameColumn = Math.Max(firstCol, dateColumns.Min(x => x.Column) - 1);

            for (var r = headerRow + 1; r <= lastRow; r++)
            {{
                var roomName = worksheet.Cell(r, roomNameColumn).GetString().Trim();
                if (string.IsNullOrWhiteSpace(roomName))
                {{
                    continue;
                }}

                foreach (var dateColumn in dateColumns)
                {{
                    if (!TryGetDecimal(worksheet.Cell(r, dateColumn.Column), out var rate))
                    {{
                        continue;
                    }}

                    yield return new ExcelRateRow(worksheet.Name, dateColumn.Date, roomName, rate, null, null);
                }}
            }}

            yield break;
        }}

        var candidateHeaderRow = firstRow;
        var roomCol = firstCol;
        var firstRateCol = firstCol + 1;
        var maxRateCols = Math.Min(numberOfDays, lastCol - firstRateCol + 1);

        if (maxRateCols <= 0)
        {{
            yield break;
        }}

        for (var r = candidateHeaderRow + 1; r <= lastRow; r++)
        {{
            var roomName = worksheet.Cell(r, roomCol).GetString().Trim();
            if (string.IsNullOrWhiteSpace(roomName))
            {{
                continue;
            }}

            for (var offset = 0; offset < maxRateCols; offset++)
            {{
                var col = firstRateCol + offset;
                if (!TryGetDecimal(worksheet.Cell(r, col), out var rate))
                {{
                    continue;
                }}

                yield return new ExcelRateRow(worksheet.Name, fallbackStartDate.AddDays(offset), roomName, rate, null, null);
            }}
        }}
    }}

    private static string NormalizeHeader(string value)
    {{
        return new string(value
            .Trim()
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
    }}

    private static bool TryGetDate(IXLCell cell, out DateOnly date)
    {{
        if (cell.DataType == XLDataType.DateTime && cell.TryGetValue<DateTime>(out var dt))
        {{
            date = DateOnly.FromDateTime(dt);
            return true;
        }}

        var text = cell.GetString().Trim();
        if (DateTime.TryParse(text, out dt))
        {{
            date = DateOnly.FromDateTime(dt);
            return true;
        }}

        date = default;
        return false;
    }}

    private static bool TryGetDecimal(IXLCell cell, out decimal value)
    {{
        if (cell.TryGetValue<decimal>(out value))
        {{
            return true;
        }}

        var text = cell.GetString().Trim().Replace(",", string.Empty);
        return decimal.TryParse(text, out value);
    }}

    private static bool TryGetInt(IXLCell cell, out int value)
    {{
        if (cell.TryGetValue<int>(out value))
        {{
            return true;
        }}

        var text = cell.GetString().Trim();
        return int.TryParse(text, out value);
    }}

    private static string MakeRoomCode(string roomName)
    {{
        var code = new string(roomName
            .Trim()
            .ToUpperInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray());

        while (code.Contains("--", StringComparison.Ordinal))
        {{
            code = code.Replace("--", "-", StringComparison.Ordinal);
        }}

        return code.Trim('-');
    }}

    private sealed record ExcelRateRow(
        string SheetName,
        DateOnly RateDate,
        string RoomTypeName,
        decimal Rate,
        int? GuestCount,
        int? RoomCount);
}}
"""

    old = read_text(path) if path.exists() else ""
    if old == content:
        return False

    backup(path)
    write_text(path, content)
    return True


def patch_controller(project_root: Path) -> bool:
    path = project_root / "Controllers" / "RatesController.cs"
    if not path.exists():
        return False

    text = read_text(path)
    original = text

    if "Excel upload is bound through ExcelImportViewModel.ExcelFile" not in text:
        text = text.replace(
            "[HttpPost]\n    [ValidateAntiForgeryToken]\n    public async Task<IActionResult> ImportExcel(ExcelImportViewModel model)",
            "[HttpPost]\n    [ValidateAntiForgeryToken]\n    // Excel upload is bound through ExcelImportViewModel.ExcelFile.\n    public async Task<IActionResult> ImportExcel(ExcelImportViewModel model)"
        )

    if text == original:
        return False

    backup(path)
    write_text(path, text)
    return True


def main() -> int:
    project_root = find_project_root(Path.cwd())
    if project_root is None:
        print("ERROR: Could not find a .csproj file.")
        print("Run this script from the ASP.NET Core project folder.")
        return 1

    csproj_files = list(project_root.glob("*.csproj"))
    csproj_path = csproj_files[0]

    print("Excel file upload patch")
    print("=" * 32)
    print(f"Project root: {project_root}")
    print(f"Project file: {csproj_path.name}")

    view_model_path = project_root / "ViewModels" / "ExcelImportViewModel.cs"
    view_model_namespace = extract_namespace(view_model_path, "RateManager.Net10.ViewModels")

    changes = []
    if ensure_package_reference(csproj_path):
        changes.append("Updated .csproj: added ClosedXML package reference.")

    if patch_view_model(project_root, view_model_namespace):
        changes.append("Updated ExcelImportViewModel.cs: added IFormFile ExcelFile.")

    if patch_import_view(project_root):
        changes.append("Updated ImportExcel.cshtml: added file picker and multipart form.")

    if patch_excel_service(project_root, view_model_namespace):
        changes.append("Updated ExcelRateImportService.cs: added uploaded file support.")

    if patch_controller(project_root):
        changes.append("Updated RatesController.cs: added upload binding comment.")

    if not changes:
        print("No changes were needed. The patch may already be applied.")
    else:
        for change in changes:
            print(change)

    print()
    print("Next steps:")
    print("1) Run the project.")
    print("2) Open: Rates / Import Excel")
    print("3) Choose the Excel file using the file picker.")
    print("4) Click Import.")
    print()
    print("If Visual Studio is already open, reload changed files when prompted.")
    print("Done.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
