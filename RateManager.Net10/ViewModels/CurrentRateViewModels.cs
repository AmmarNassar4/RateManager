using Microsoft.AspNetCore.Mvc.Rendering;

namespace RateManager.Net10.ViewModels;

public class CurrentRateCalculatorViewModel
{
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public int Nights { get; set; } = 1;
    public int RatePlanId { get; set; }
    public int RoomTypeId { get; set; }
    public int RoomCount { get; set; } = 1;
    public bool IncludeBreakfast { get; set; }
    public int BreakfastPeople { get; set; } = 0;
    public bool IncludeLunch { get; set; }
    public int LunchPeople { get; set; } = 0;

    public decimal BreakfastPrice { get; set; }
    public decimal LunchPrice { get; set; }
    public decimal TaxPercent { get; set; }

    public List<SelectListItem> RatePlans { get; set; } = new();
    public List<SelectListItem> RoomTypes { get; set; } = new();
}

public class CurrentRateCalculationRequest
{
    public int RatePlanId { get; set; }
    public int RoomTypeId { get; set; }
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public int Nights { get; set; } = 1;
    public int RoomCount { get; set; } = 1;
    public bool IncludeBreakfast { get; set; }
    public int BreakfastPeople { get; set; }
    public bool IncludeLunch { get; set; }
    public int LunchPeople { get; set; }
}

public class CurrentRateCalculationResponse
{
    public int RatePlanId { get; set; }
    public string RatePlanName { get; set; } = string.Empty;
    public int RoomTypeId { get; set; }
    public string RoomTypeName { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public int Nights { get; set; }
    public int RoomCount { get; set; }
    public decimal RoomTotal { get; set; }
    public decimal BreakfastPrice { get; set; }
    public int BreakfastPeople { get; set; }
    public decimal BreakfastTotal { get; set; }
    public decimal LunchPrice { get; set; }
    public int LunchPeople { get; set; }
    public decimal LunchTotal { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public List<CurrentRateNightLine> NightsBreakdown { get; set; } = new();
}

public class CurrentRateNightLine
{
    public DateOnly Date { get; set; }
    public decimal RoomRate { get; set; }
    public int RoomCount { get; set; }
    public decimal RoomTotal { get; set; }
}
