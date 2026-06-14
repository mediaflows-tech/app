using MediaFlows.Shared.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediaFlows.Web.Controllers.Api;

[Route("api/v1/admin/monitoring")]
[Authorize(Policy = "AdminOnly")]
public class MonitoringApiController : ApiBaseController
{
    private readonly IAnalyticsService _analyticsService;

    public MonitoringApiController(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    [HttpGet("")]
    public async Task<IActionResult> GetSnapshot()
    {
        var snapshot = await _analyticsService.GetCurrentSnapshotAsync();
        return Ok(snapshot);
    }

    [HttpGet("alarms")]
    public async Task<IActionResult> GetAlarms()
    {
        var snapshot = await _analyticsService.GetCloudWatchMetricsAsync();
        var alarms = snapshot.ActiveAlarms.Select(a => new
        {
            a.AlarmName,
            a.StateValue,
            a.MetricName,
            updated = a.StateUpdatedTimestamp.ToString("MMM d, HH:mm")
        });
        return Ok(alarms);
    }
}
