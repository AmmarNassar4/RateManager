using RateManager.Net10.ViewModels;

namespace RateManager.Net10.Services;

public interface IRateCalculationService
{
    Task<int> GenerateRatesAsync(GenerateRatesViewModel model, string? userName);
    Task UpdateManualRateAsync(ManualRateUpdateInput input, string? userName);
}
