using SACCOBlockChainSystem.Models;

namespace SACCOBlockChainSystem.Services
{
    public interface IAuditService
    {
        Task LogActivityAsync(string tableName, string recordId, string action,
            string? oldValues, string? newValues, string userId, string userName);
        Task<List<AuditLog>> GetAuditLogsAsync(DateTime? fromDate, DateTime? toDate, string? userId);
        Task<List<AuditLog>> GetTableAuditLogsAsync(string tableName, string? recordId);
    }
}