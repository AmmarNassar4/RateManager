using RateManager.Net10.ViewModels;

namespace RateManager.Net10.Services;

public interface IExcelRateImportService
{
    Task<int> ImportAsync(ExcelImportViewModel model, string? userName);
}
