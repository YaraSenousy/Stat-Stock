using StatStock.Domain.Entities;

namespace StatStock.Application.Interfaces;

public interface IAuditService
{
    Task LogAsync(string userId, string userEmail, string action, string entityType, string entityId, 
        string? oldValues = null, string? newValues = null, string ipAddress = "");
    
    Task<IEnumerable<AuditLog>> GetLogsAsync(DateTime? startDate = null, DateTime? endDate = null, 
        string? userId = null, string? entityType = null, int pageSize = 100, int page = 1);
}
