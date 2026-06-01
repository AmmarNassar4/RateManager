namespace RateManager.Net10.Models;

public class PmsRoomTypeMapping
{
    public int PmsRoomTypeMappingId { get; set; }
    public int RoomTypeId { get; set; }
    public string PmsRoomTypeCode { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public RoomType? RoomType { get; set; }
}
