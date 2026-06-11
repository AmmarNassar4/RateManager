namespace RateManager.Net10.Services;

public class SharePointExcelSyncState
{
    public DateTimeOffset? LastRunAt { get; set; }
    public bool LastRunEnabled { get; set; }
    public string LastStatus { get; set; } = string.Empty;
    public string LastConfiguredDriveId { get; set; } = string.Empty;
    public string LastConfiguredFilePath { get; set; } = string.Empty;
    public string LastMetadataUrl { get; set; } = string.Empty;
    public string LastDownloadUrl { get; set; } = string.Empty;
    public string LastError { get; set; } = string.Empty;
    public string LastItemId { get; set; } = string.Empty;
    public string LastETag { get; set; } = string.Empty;
    public DateTimeOffset? LastModifiedDateTime { get; set; }
    public DateTimeOffset? LastSuccessfulImportAt { get; set; }
    public int? LastBatchId { get; set; }
    public string LastMessage { get; set; } = string.Empty;
}
