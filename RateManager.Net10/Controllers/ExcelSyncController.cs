using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RateManager.Net10.Services;
using RateManager.Net10.ViewModels;

namespace RateManager.Net10.Controllers;

[ApiController]
public class ExcelSyncController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IExcelRateImportService _excelImportService;

    public ExcelSyncController(IConfiguration configuration, IExcelRateImportService excelImportService)
    {
        _configuration = configuration;
        _excelImportService = excelImportService;
    }

    [AllowAnonymous]
    [HttpPost("api/excel-sync/import")]
    public async Task<IActionResult> Import([FromBody] ExcelSyncImportRequest request)
    {
        if (!IsAuthorizedForImport())
        {
            return Unauthorized(new ExcelSyncImportResponse
            {
                Success = false,
                Message = "Unauthorized. Send a valid X-Import-Key header or sign in as Admin."
            });
        }

        if (request.RatePlanId <= 0)
        {
            return BadRequest(new ExcelSyncImportResponse { Success = false, Message = "RatePlanId is required." });
        }

        if (request.NumberOfDays <= 0)
        {
            return BadRequest(new ExcelSyncImportResponse { Success = false, Message = "NumberOfDays must be greater than zero." });
        }

        if (string.IsNullOrWhiteSpace(request.FileContentBase64))
        {
            return BadRequest(new ExcelSyncImportResponse { Success = false, Message = "FileContentBase64 is required." });
        }

        string tempFilePath;
        try
        {
            tempFilePath = await SaveBase64ExcelToTempFileAsync(request.FileName, request.FileContentBase64);
        }
        catch (Exception ex)
        {
            return BadRequest(new ExcelSyncImportResponse { Success = false, Message = $"Invalid file content: {ex.Message}" });
        }

        try
        {
            var model = new ExcelImportViewModel
            {
                RatePlanId = request.RatePlanId,
                DiscountRatePlanId = request.DiscountRatePlanId,
                StartDate = request.StartDate,
                NumberOfDays = request.NumberOfDays,
                DefaultGuestCount = request.DefaultGuestCount <= 0 ? 2 : request.DefaultGuestCount,
                DefaultRoomCount = request.DefaultRoomCount <= 0 ? 1 : request.DefaultRoomCount,
                ExcelFilePath = tempFilePath,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? "Imported by Excel Sync API" : request.Notes
            };

            var batchId = await _excelImportService.ImportAsync(model, User.Identity?.Name ?? "ExcelSyncApi");

            return Ok(new ExcelSyncImportResponse
            {
                Success = true,
                BatchId = batchId,
                Message = "Excel file imported successfully."
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new ExcelSyncImportResponse
            {
                Success = false,
                Message = ex.Message
            });
        }
        finally
        {
            if (System.IO.File.Exists(tempFilePath))
            {
                try
                {
                    System.IO.File.Delete(tempFilePath);
                }
                catch
                {
                    // Ignore temp cleanup errors.
                }
            }
        }
    }

    private bool IsAuthorizedForImport()
    {
        if (User.Identity?.IsAuthenticated == true && User.IsInRole("Admin"))
        {
            return true;
        }

        var configuredKey = _configuration["ExcelSync:ImportKey"];
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            return false;
        }

        var providedKey = Request.Headers["X-Import-Key"].FirstOrDefault();
        return string.Equals(providedKey, configuredKey, StringComparison.Ordinal);
    }

    private static async Task<string> SaveBase64ExcelToTempFileAsync(string fileName, string fileContentBase64)
    {
        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".xlsx";
        }

        extension = extension.ToLowerInvariant();
        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".xlsx",
            ".xlsm",
            ".xltx",
            ".xltm"
        };

        if (!allowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("Unsupported Excel file type. Please send .xlsx or .xlsm file content.");
        }

        var bytes = Convert.FromBase64String(fileContentBase64);
        if (bytes.Length == 0)
        {
            throw new InvalidOperationException("The Excel file is empty.");
        }

        var folder = Path.Combine(Path.GetTempPath(), "RateManagerExcelSync");
        Directory.CreateDirectory(folder);

        var tempFilePath = Path.Combine(folder, $"{Guid.NewGuid():N}{extension}");
        await System.IO.File.WriteAllBytesAsync(tempFilePath, bytes);
        return tempFilePath;
    }
}
