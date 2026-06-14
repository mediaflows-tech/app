using MediaFlows.Shared.DTOs;
using MediaFlows.Shared.Models.Entities;

namespace MediaFlows.Shared.Interfaces;

public interface IAuditLogService
{
    Task LogAsync(string action, string entityType, string entityId, object? details = null);
    Task<PagedResult<AuditLog>> SearchAsync(
        string? query, string? userId, string? actionType,
        DateTime? from, DateTime? to, int page, int pageSize);
    Task<List<string>> GetDistinctActionTypesAsync();
}
