using MediaFlows.Shared.Interfaces;
using MediaFlows.Web.Controllers.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediaFlows.Web.Controllers.Api;

[Route("api/v1/admin/summary")]
[Authorize(Policy = "AdminOnly")]
public class AdminSummaryApiController : ApiBaseController
{
    private readonly IAnalyticsService _analyticsService;

    public AdminSummaryApiController(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    [HttpGet("")]
    public async Task<IActionResult> GetSummary()
    {
        var snapshot = await _analyticsService.GetCurrentSnapshotAsync();
        var dailyUploads = await _analyticsService.GetDailyUploadCountsAsync(7);
        var storageByType = await _analyticsService.GetStorageByTypeAsync();
        var viewModel = AdminDashboardViewModel.FromSnapshot(snapshot, dailyUploads, storageByType);
        return Ok(viewModel);
    }

    [HttpGet("activity")]
    public async Task<IActionResult> GetActivity(int days = 7)
    {
        if (days is not (7 or 30 or 90)) days = 7;
        var dailyUploads = await _analyticsService.GetDailyUploadCountsAsync(days);
        return Ok(new
        {
            labels = dailyUploads.Select(d => d.Date.ToString(days <= 7 ? "ddd" : "MMM d")),
            data = dailyUploads.Select(d => d.Count)
        });
    }
}
