using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;

namespace SACCOBlockChainSystem.Services
{
    public class AuditService : IAuditService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AuditService> _logger;

        public AuditService(ApplicationDbContext context, ILogger<AuditService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task LogActivityAsync(string tableName, string recordId, string action,
            string? oldValues, string? newValues, string userId, string userName)
        {
            try
            {
                var auditLog = new AuditLog
                {
                    TableName = tableName,
                    RecordId = recordId,
                    Action = action,
                    OldValues = oldValues,
                    NewValues = newValues,
                    UserId = userId,
                    UserName = userName,
                    Timestamp = DateTime.UtcNow
                };

                await _context.AuditLogs.AddAsync(auditLog);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging audit trail");
                // Don't throw - audit failure shouldn't break main functionality
            }
        }

        public async Task<List<AuditLog>> GetAuditLogsAsync(DateTime? fromDate, DateTime? toDate, string? userId)
        {
            var query = _context.AuditLogs.AsQueryable();

            if (fromDate.HasValue)
                query = query.Where(a => a.Timestamp >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(a => a.Timestamp <= toDate.Value);

            if (!string.IsNullOrEmpty(userId))
                query = query.Where(a => a.UserId == userId);

            return await query
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();
        }

        public async Task<List<AuditLog>> GetTableAuditLogsAsync(string tableName, string? recordId)
        {
            var query = _context.AuditLogs
                .Where(a => a.TableName == tableName);

            if (!string.IsNullOrEmpty(recordId))
                query = query.Where(a => a.RecordId == recordId);

            return await query
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();
        }
    }
}