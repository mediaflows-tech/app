using MediaFlows.Shared.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediaFlows.Web.Controllers.Api;

[Route("api/v1/admin/audit-logs")]
[Authorize(Policy = "AdminOnly")]
public class AuditLogsApiController : ApiBaseController
{
    private readonly IAuditLogService _auditLogService;

    public AuditLogsApiController(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    /// <summary>GET api/v1/admin/audit-logs — search audit logs</summary>
    [HttpGet("")]
    public async Task<IActionResult> SearchLogs(
        string? query = null, string? userId = null, string? actionType = null,
        string? from = null, string? to = null, int page = 1)
    {
        DateTime? fromDate = DateTime.TryParse(from, out var f) ? DateTime.SpecifyKind(f, DateTimeKind.Utc) : null;
        DateTime? toDisplay = DateTime.TryParse(to, out var t) ? DateTime.SpecifyKind(t.Date, DateTimeKind.Utc) : null;
        DateTime? toDate = toDisplay?.AddDays(1).AddTicks(-1);

        var result = await _auditLogService.SearchAsync(query, userId, actionType, fromDate, toDate, page, 20);
        var actionTypes = await _auditLogService.GetDistinctActionTypesAsync();

        return Ok(new
        {
            logs = result,
            actionTypes
        });
    }
}
