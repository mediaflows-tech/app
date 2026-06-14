using System.Security.Claims;
using System.Text.Json;
using MediaFlows.Data;
using MediaFlows.Shared.DTOs;
using MediaFlows.Shared.Interfaces;
using MediaFlows.Shared.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MediaFlows.Web.Services;

public class AuditLogService : IAuditLogService
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditLogService(ApplicationDbContext db, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogAsync(string action, string entityType, string entityId, object? details = null)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details != null ? JsonSerializer.Serialize(details) : null,
            UserId = _httpContextAccessor.HttpContext?.User.FindFirstValue("sub"),
            UserEmail = _httpContextAccessor.HttpContext?.User.FindFirstValue("email"),
            IpAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            Timestamp = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    public async Task<PagedResult<AuditLog>> SearchAsync(
        string? query, string? userId, string? actionType,
        DateTime? from, DateTime? to, int page, int pageSize)
    {
        var q = _db.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var escaped = query.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
            var pattern = $"%{escaped}%";
            q = q.Where(l =>
                EF.Functions.ILike(l.Action, pattern) ||
                EF.Functions.ILike(l.EntityType, pattern) ||
                EF.Functions.ILike(l.EntityId, pattern) ||
                (l.UserEmail != null && EF.Functions.ILike(l.UserEmail, pattern)) ||
                (l.Details != null && EF.Functions.ILike(l.Details, pattern)));
        }

        if (!string.IsNullOrEmpty(userId))
            q = q.Where(l => l.UserId == userId);

        if (!string.IsNullOrEmpty(actionType))
            q = q.Where(l => l.Action == actionType);

        if (from.HasValue)
            q = q.Where(l => l.Timestamp >= from.Value);

        if (to.HasValue)
            q = q.Where(l => l.Timestamp <= to.Value);

        var totalCount = await q.CountAsync();

        var items = await q
            .OrderByDescending(l => l.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<AuditLog>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            HasMore = (page * pageSize) < totalCount
        };
    }

    public async Task<List<string>> GetDistinctActionTypesAsync()
    {
        return await _db.AuditLogs
            .Select(l => l.Action)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync();
    }
}
