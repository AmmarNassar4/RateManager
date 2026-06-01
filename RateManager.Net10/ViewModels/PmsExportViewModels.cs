using Microsoft.AspNetCore.Mvc.Rendering;
using RateManager.Net10.Models;

namespace RateManager.Net10.ViewModels;

public class PmsExportViewModel
{
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public int NumberOfDays { get; set; } = 30;
    public int RatePlanId { get; set; }
    public decimal FbMealPrice { get; set; }
    public bool ExportRo { get; set; } = true;
    public bool ExportBb { get; set; } = true;
    public bool ExportHb { get; set; } = true;
    public bool ExportFb { get; set; }
    public List<PmsRoomTypeMappingInput> RoomMappings { get; set; } = new();
    public List<SelectListItem> RatePlans { get; set; } = new();
}

public class PmsRoomTypeMappingInput
{
    public int RoomTypeId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public string RoomCode { get; set; } = string.Empty;
    public string PmsRoomTypeCode { get; set; } = string.Empty;
}

public class PmsExportResultViewModel
{
    public int InsertedRows { get; set; }
    public int UpdatedRows { get; set; }
    public int SkippedRows { get; set; }
    public List<string> Messages { get; set; } = new();
}

public class PmsExportRow
{
    public int TrfTbl { get; set; } = 9;
    public string PropertyCode { get; set; } = "RRK";
    public string RoomTypeCode { get; set; } = string.Empty;
    public string FromDate { get; set; } = string.Empty;
    public string ToDate { get; set; } = string.Empty;
    public string PlanCode { get; set; } = "RO";
    public int DayCode { get; set; }
    public string CurrencyCode { get; set; } = "SAR";
    public string Description { get; set; } = "Rate Manager Export";
    public string TableType { get; set; } = "R";
    public decimal SingleRate { get; set; }
    public decimal DoubleRate { get; set; }
    public decimal TripleRate { get; set; }
    public decimal FourPersonRate { get; set; }
    public decimal AdultRate { get; set; }
    public decimal ChildRate { get; set; }
    public string FoodCurrencyCode { get; set; } = "SAR";
    public decimal FoodSingleRate { get; set; }
    public decimal FoodDoubleRate { get; set; }
    public decimal FoodTripleRate { get; set; }
    public decimal FoodFourPersonRate { get; set; }
    public decimal FoodAdultRate { get; set; }
    public decimal FoodChildRate { get; set; }
    public string UserId { get; set; } = "RMAPP";
}
