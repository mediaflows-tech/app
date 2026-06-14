using MediaFlows.Shared.Interfaces;
using MediaFlows.Shared.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediaFlows.Web.Controllers.Api;

[Route("api/v1/assets")]
[Authorize(Policy = "CanCreateContent")]
public class AssetsApiController : ApiBaseController
{
    private readonly IMediaAssetService _assetService;
    private readonly ICommentService _commentService;
    private readonly IS3StorageService _s3Service;

    public AssetsApiController(
        IMediaAssetService assetService,
        ICommentService commentService,
        IS3StorageService s3Service)
    {
        _assetService = assetService;
        _commentService = commentService;
        _s3Service = s3Service;
    }

    [HttpGet("")]
    public async Task<IActionResult> GetAssets(
        int page = 1,
        AssetStatus? status = null,
        string? fileType = null,
        string? sort = null)
    {
        var result = await _assetService.GetPagedAssetsAsync(
            CurrentUserId, status, page, pageSize: 20, fileType, sort);

        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetAsset(int id)
    {
        var asset = await _assetService.GetByIdAsync(id);
        if (asset == null)
            return ApiNotFound("Asset");

        if (asset.CreatorId != CurrentUserId)
            return Forbid();

        var mediaUrl = _s3Service.GetPublicUrl(asset.S3Key);
        object? comments = null;
        int commentCount = 0;
        try { comments = await _commentService.GetCommentsAsync(id, CurrentUserId); } catch { }
        try { commentCount = await _commentService.GetCommentCountAsync(id); } catch { }

        return Ok(new
        {
            asset = new
            {
                asset.Id,
                asset.Title,
                asset.Description,
                asset.S3Key,
                asset.ThumbnailUrl,
                asset.ContentType,
                asset.FileSize,
                asset.Status,
                asset.CreatorId,
                CreatorName = asset.Creator?.DisplayName ?? asset.Creator?.Email ?? "Unknown",
                asset.Metadata,
                VersionCount = asset.Versions?.Count ?? 0,
                asset.CurrentVersionId,
                asset.ScheduledPublishAt,
                asset.PublishedAt,
                asset.CreatedAt,
                asset.UpdatedAt
            },
            mediaUrl,
            comments = comments ?? new List<object>(),
            commentCount
        });
    }

    [HttpPatch("{id:int}")]
    public async Task<IActionResult> UpdateAsset(int id, [FromBody] UpdateAssetRequest request)
    {
        var asset = await _assetService.GetByIdAsync(id);
        if (asset == null)
            return ApiNotFound("Asset");

        if (asset.CreatorId != CurrentUserId)
            return Forbid();

        if (request.Title != null)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                return ApiError("Title cannot be empty");

            await _assetService.UpdateTitleAsync(id, request.Title.Trim());
        }

        if (request.Description != null)
            await _assetService.UpdateDescriptionAsync(id, request.Description.Trim());

        var updated = await _assetService.GetByIdAsync(id);
        return Ok(new { id = updated!.Id, title = updated.Title, description = updated.Description });
    }

    [HttpPost("{id:int}/submit")]
    public async Task<IActionResult> SubmitForReview(int id)
    {
        var asset = await _assetService.GetByIdAsync(id);
        if (asset == null)
            return ApiNotFound("Asset");

        if (asset.CreatorId != CurrentUserId)
            return Forbid();

        if (asset.Status != AssetStatus.Draft)
            return ApiError($"Asset must be in Draft status to submit. Current status: {asset.Status}");

        await _assetService.UpdateStatusAsync(id, AssetStatus.PendingReview, CurrentUserId);

        return Ok(new { id, status = AssetStatus.PendingReview.ToString() });
    }

    [HttpPost("bulk/submit")]
    public async Task<IActionResult> BulkSubmit([FromBody] BulkAssetActionRequest request)
    {
        int submitted = 0, skipped = 0;
        foreach (var id in request.AssetIds)
        {
            var asset = await _assetService.GetByIdAsync(id);
            if (asset == null || asset.CreatorId != CurrentUserId) { skipped++; continue; }
            if (asset.Status != AssetStatus.Draft) { skipped++; continue; }
            await _assetService.UpdateStatusAsync(id, AssetStatus.PendingReview, CurrentUserId);
            submitted++;
        }
        return Ok(new { submitted, skipped });
    }

    [HttpDelete("bulk")]
    public async Task<IActionResult> BulkDelete([FromBody] BulkAssetActionRequest request)
    {
        int deleted = 0, skipped = 0;
        foreach (var id in request.AssetIds)
        {
            var asset = await _assetService.GetByIdAsync(id);
            if (asset == null || asset.CreatorId != CurrentUserId) { skipped++; continue; }
            if (asset.Status != AssetStatus.Draft && asset.Status != AssetStatus.Rejected) { skipped++; continue; }
            await _assetService.DeleteAsync(id, CurrentUserId);
            deleted++;
        }
        return Ok(new { deleted, skipped });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsset(int id)
    {
        var asset = await _assetService.GetByIdAsync(id);
        if (asset == null)
            return ApiNotFound("Asset");

        if (asset.CreatorId != CurrentUserId)
            return Forbid();

        await _assetService.DeleteAsync(id, CurrentUserId);

        return Ok(new { id, deleted = true });
    }
}

public class BulkAssetActionRequest
{
    public int[] AssetIds { get; set; } = Array.Empty<int>();
}

public class UpdateAssetRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
}
