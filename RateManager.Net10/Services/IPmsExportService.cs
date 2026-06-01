using RateManager.Net10.ViewModels;

namespace RateManager.Net10.Services;

public interface IPmsExportService
{
    Task<PmsExportResultViewModel> ExportAsync(PmsExportViewModel model, string? userName);
}
