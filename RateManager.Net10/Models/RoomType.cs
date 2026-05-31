namespace RateManager.Net10.Models;

public class RoomType
{
    public int RoomTypeId { get; set; }
    public string RoomCode { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<DailyRoomRate> DailyRoomRates { get; set; } = new List<DailyRoomRate>();
}
