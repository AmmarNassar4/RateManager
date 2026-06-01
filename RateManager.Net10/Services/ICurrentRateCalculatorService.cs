using RateManager.Net10.ViewModels;

namespace RateManager.Net10.Services;

public interface ICurrentRateCalculatorService
{
    Task<CurrentRateCalculationResponse> CalculateAsync(CurrentRateCalculationRequest request);
}
