namespace RateManager.Net10.Services;

public class SharePointExcelSyncState
{
    public string LastItemId { get; set; } = string.Empty;
    public string LastETag { get; set; } = string.Empty;
    public DateTimeOffset? LastModifiedDateTime { get; set; }
    public DateTimeOffset? LastSuccessfulImportAt { get; set; }
    public int? LastBatchId { get; set; }
    public string LastMessage { get; set; } = string.Empty;
}
