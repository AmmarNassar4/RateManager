using RateManager.Net10.Data;
using RateManager.Net10.Models;

namespace RateManager.Net10.Services;

public class RateAuditService : IRateAuditService
{
    private readonly AppDbContext _db;

    public RateAuditService(AppDbContext db)
    {
        _db = db;
    }

    public async Task WriteAsync(string entityName, long entityId, string actionType, string? fieldName, string? oldValue, string? newValue, string? userName)
    {
        _db.RateAuditLogs.Add(new RateAuditLog
        {
            EntityName = entityName,
            EntityId = entityId,
            ActionType = actionType,
            FieldName = fieldName,
            OldValue = oldValue,
            NewValue = newValue,
            CreatedBy = userName
        });

        await _db.SaveChangesAsync();
    }
}
