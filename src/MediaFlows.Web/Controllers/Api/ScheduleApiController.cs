using MediaFlows.Shared.DTOs;
using MediaFlows.Shared.Interfaces;
using MediaFlows.Shared.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediaFlows.Web.Controllers.Api;

[Route("api/v1/schedule")]
[Authorize(Policy = "CanReview")]
public class ScheduleApiController : ApiBaseController
{
    private readonly IReviewService _reviewService;

    public ScheduleApiController(IReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    /// <summary>GET api/v1/schedule/events?start&amp;end — scheduled events for FullCalendar</summary>
    [HttpGet("events")]
    public async Task<IActionResult> GetEvents(DateTime start, DateTime end)
    {
        var assets = await _reviewService.GetScheduledAssetsAsync(start, end);

        var events = assets.Select(a => new
        {
            id = a.AssetId.ToString(),
            title = a.Title,
            start = a.ScheduledPublishAt?.ToString("o"),
            backgroundColor = GetStatusColor(a.Status),
            extendedProps = new
            {
                assetId = a.AssetId,
                thumbnailUrl = a.ThumbnailUrl ?? "",
                status = a.Status.ToString()
            }
        });

        return Ok(events);
    }

    /// <summary>GET api/v1/schedule/available — assets available for scheduling</summary>
    [HttpGet("available")]
    public async Task<IActionResult> GetAvailable()
    {
        var assets = await _reviewService.GetAvailableAssetsAsync();
        return Ok(assets.Select(a => new { id = a.AssetId, title = a.Title }));
    }

    /// <summary>POST api/v1/schedule — create a schedule for an asset</summary>
    [HttpPost("")]
    public async Task<IActionResult> Create([FromBody] ScheduleUpdateRequest request)
    {
        if (!ModelState.IsValid)
            return ApiError(string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));

        try
        {
            await _reviewService.SchedulePublishAsync(request.AssetId, request.ScheduledPublishAt, CurrentUserId);
            return Ok(new { success = true });
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return ApiError(ex.Message);
        }
    }

    /// <summary>PUT api/v1/schedule/{assetId} — update an existing schedule</summary>
    [HttpPut("{assetId:int}")]
    public async Task<IActionResult> Update(int assetId, [FromBody] UpdateScheduleRequest request)
    {
        if (!ModelState.IsValid)
            return ApiError(string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));

        try
        {
            await _reviewService.ReschedulePublishAsync(assetId, request.ScheduledPublishAt, CurrentUserId);
            return Ok(new { success = true });
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return ApiError(ex.Message);
        }
    }

    /// <summary>DELETE api/v1/schedule/{assetId} — remove an asset's schedule</summary>
    [HttpDelete("{assetId:int}")]
    public async Task<IActionResult> Unschedule(int assetId)
    {
        try
        {
            await _reviewService.UnscheduleAsync(assetId, CurrentUserId);
            return Ok(new { success = true });
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return ApiError(ex.Message);
        }
    }

    private static string GetStatusColor(AssetStatus status) => status switch
    {
        AssetStatus.Approved => "#198754",
        AssetStatus.Published => "#0d6efd",
        AssetStatus.PendingReview => "#ffc107",
        AssetStatus.Submitted => "#0d6efd",
        AssetStatus.Rejected => "#dc3545",
        AssetStatus.ChangesRequested => "#fd7e14",
        _ => "#6c757d"
    };
}

// ── Request DTO local to this controller ───────────────────────────────────

public class UpdateScheduleRequest
{
    public DateTime ScheduledPublishAt { get; set; }
}
