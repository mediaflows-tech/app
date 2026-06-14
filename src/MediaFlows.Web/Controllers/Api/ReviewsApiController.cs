using MediaFlows.Shared.DTOs;
using MediaFlows.Shared.Interfaces;
using MediaFlows.Shared.Models.Enums;
using MediaFlows.Web.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediaFlows.Web.Controllers.Api;

[Route("api/v1/reviews")]
[Authorize(Policy = "CanReview")]
[HandleServiceExceptions]
public class ReviewsApiController : ApiBaseController
{
    private readonly IReviewService _reviewService;
    private readonly IS3StorageService _s3Service;

    public ReviewsApiController(IReviewService reviewService, IS3StorageService s3Service)
    {
        _reviewService = reviewService;
        _s3Service = s3Service;
    }

    [HttpGet("")]
    public async Task<IActionResult> GetReviews(
        int page = 1,
        AssetStatus? status = null,
        string? contentType = null,
        string? sortBy = null,
        string? sortDir = null)
    {
        var reviews = await _reviewService.GetPendingReviewsAsync(
            status, creatorId: null, contentType, sortBy, sortDir, page, pageSize: 20);

        var pendingCount = await _reviewService.GetPendingCountAsync();
        var approvedCount = await _reviewService.GetApprovedCountAsync();
        var rejectedCount = await _reviewService.GetRejectedCountAsync();

        return Ok(new
        {
            items = reviews.Items,
            totalCount = reviews.TotalCount,
            page = reviews.Page,
            pageSize = reviews.PageSize,
            totalPages = reviews.TotalPages,
            hasMore = reviews.HasMore,
            counts = new
            {
                pending = pendingCount,
                approved = approvedCount,
                rejected = rejectedCount
            }
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetReview(int id)
    {
        var asset = await _reviewService.GetReviewDetailsAsync(id);
        if (asset is null)
            return ApiNotFound("Review");

        var mediaUrl = _s3Service.GetPublicUrl(asset.S3Key);
        var canReview = asset.Status is AssetStatus.PendingReview or AssetStatus.Submitted;
        var canSchedule = asset.Status == AssetStatus.Approved && !asset.ScheduledPublishAt.HasValue;

        return Ok(new
        {
            asset,
            mediaUrl,
            canReview,
            canSchedule
        });
    }

    [HttpPost("{assetId:int}/decide")]
    public async Task<IActionResult> Decide(int assetId, [FromBody] ReviewDecisionRequest request)
    {
        if (ValidateModelState() is { } invalid) return invalid;

        if (request.Decision == ReviewDecision.Approved && request.PublishImmediately)
        {
            await _reviewService.ApproveAndPublishAsync(assetId, CurrentUserId, request.Comments);
        }
        else if (request.Decision == ReviewDecision.Approved && request.ScheduledPublishAt.HasValue)
        {
            await _reviewService.ApproveAndScheduleAsync(
                assetId, request.ScheduledPublishAt.Value, CurrentUserId, request.Comments);
        }
        else
        {
            await _reviewService.SubmitDecisionAsync(assetId, request.Decision, CurrentUserId, request.Comments);
        }

        return Ok(new { success = true, message = $"Review decision submitted: {request.Decision}" });
    }

    [HttpPost("{assetId:int}/publish")]
    public async Task<IActionResult> PublishNow(int assetId)
    {
        await _reviewService.PublishNowAsync(assetId, CurrentUserId);
        return Ok(new { success = true, message = "Asset published immediately." });
    }

    [HttpPost("{assetId:int}/reject")]
    public async Task<IActionResult> RejectApproved(int assetId, [FromBody] RejectApprovedRequest request)
    {
        if (ValidateModelState() is { } invalid) return invalid;

        if (string.IsNullOrWhiteSpace(request.Comments))
            return ApiError("A reason is required when rejecting an asset.");

        await _reviewService.RejectApprovedAsync(assetId, CurrentUserId, request.Comments);
        return Ok(new { success = true, message = "Asset rejected." });
    }

    [HttpPost("batch/decide")]
    public async Task<IActionResult> BatchDecide([FromBody] BatchReviewDecisionRequest request)
    {
        if (ValidateModelState() is { } invalid) return invalid;

        if (request.AssetIds.Length == 0)
            return ApiError("No assets selected.");

        await _reviewService.BatchDecisionAsync(request.AssetIds, request.Decision, CurrentUserId, request.Comments);
        return Ok(new { success = true, message = "Batch review completed.", count = request.AssetIds.Length });
    }

    [HttpPost("batch/publish")]
    public async Task<IActionResult> BatchPublish([FromBody] BatchPublishRequest request)
    {
        if (ValidateModelState() is { } invalid) return invalid;

        if (request.AssetIds.Length == 0)
            return ApiError("No assets selected.");

        var processed = await _reviewService.BatchApproveAndPublishAsync(request.AssetIds, CurrentUserId, request.Comments);
        var skipped = request.AssetIds.Length - processed;
        return Ok(new
        {
            success = true,
            message = "Assets approved and published.",
            count = processed,
            skipped
        });
    }

    [HttpPost("batch/approve-schedule")]
    public async Task<IActionResult> BatchApproveAndSchedule([FromBody] BatchScheduleRequest request)
    {
        if (ValidateModelState() is { } invalid) return invalid;

        if (request.AssetIds.Length == 0)
            return ApiError("No assets selected.");

        string? warningMessage = null;
        try
        {
            await _reviewService.BatchApproveAndScheduleAsync(request.AssetIds, request.ScheduledPublishAt, CurrentUserId);
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("Scheduled"))
        {
            warningMessage = ex.Message;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return ApiError(ex.Message);
        }

        return Ok(new
        {
            success = true,
            warning = warningMessage,
            message = warningMessage ?? "Assets approved and scheduled.",
            count = request.AssetIds.Length
        });
    }

    [HttpPost("batch/schedule")]
    public async Task<IActionResult> BatchSchedule([FromBody] BatchScheduleRequest request)
    {
        if (ValidateModelState() is { } invalid) return invalid;

        if (request.AssetIds.Length == 0)
            return ApiError("No assets selected.");

        string? warningMessage = null;
        try
        {
            await _reviewService.BatchScheduleAsync(request.AssetIds, request.ScheduledPublishAt, CurrentUserId);
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("Scheduled"))
        {
            warningMessage = ex.Message;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return ApiError(ex.Message);
        }

        return Ok(new
        {
            success = true,
            warning = warningMessage,
            message = warningMessage ?? "Assets scheduled for publishing.",
            count = request.AssetIds.Length
        });
    }
}
