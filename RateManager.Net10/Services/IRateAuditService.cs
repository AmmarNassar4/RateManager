namespace RateManager.Net10.Services;

public interface IRateAuditService
{
    Task WriteAsync(string entityName, long entityId, string actionType, string? fieldName, string? oldValue, string? newValue, string? userName);
}
