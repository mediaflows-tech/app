using MediaFlows.Shared.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediaFlows.Web.Controllers.Api;

[Route("api/v1/assets/{id:int}/tags")]
[Authorize(Policy = "CanCreateContent")]
public class TagsApiController : ApiBaseController
{
    private readonly IMediaAssetService _assetService;

    public TagsApiController(IMediaAssetService assetService)
    {
        _assetService = assetService;
    }

    /// <summary>GET api/v1/assets/{id}/tags — get manual and auto tags for an asset</summary>
    [HttpGet("")]
    public async Task<IActionResult> GetTags(int id)
    {
        var asset = await _assetService.GetByIdAsync(id);
        if (asset == null)
            return ApiNotFound("Asset");

        if (asset.CreatorId != CurrentUserId)
            return Forbid();

        return Ok(new
        {
            assetId = id,
            manualTags = asset.Metadata.Tags,
            autoTags = asset.Metadata.AutoTags.Select(t => new { t.Name, t.Confidence })
        });
    }

    /// <summary>POST api/v1/assets/{id}/tags — add a manual tag</summary>
    [HttpPost("")]
    public async Task<IActionResult> AddTag(int id, [FromBody] AddTagRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.TagName))
            return ApiError("tagName is required");

        var asset = await _assetService.GetByIdAsync(id);
        if (asset == null)
            return ApiNotFound("Asset");

        if (asset.CreatorId != CurrentUserId)
            return Forbid();

        var normalizedTag = request.TagName.Trim().ToLower();

        if (asset.Metadata.Tags.Contains(normalizedTag))
            return ApiError($"Tag '{normalizedTag}' already exists on this asset");

        await _assetService.AddTagAsync(id, normalizedTag);

        var updated = await _assetService.GetByIdAsync(id);
        return Ok(new
        {
            assetId = id,
            manualTags = updated!.Metadata.Tags,
            added = normalizedTag
        });
    }

    /// <summary>DELETE api/v1/assets/{id}/tags/{tagName} — remove a manual tag</summary>
    [HttpDelete("{tagName}")]
    public async Task<IActionResult> RemoveTag(int id, string tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName))
            return ApiError("tagName is required");

        var asset = await _assetService.GetByIdAsync(id);
        if (asset == null)
            return ApiNotFound("Asset");

        if (asset.CreatorId != CurrentUserId)
            return Forbid();

        await _assetService.RemoveTagAsync(id, tagName);

        var updated = await _assetService.GetByIdAsync(id);
        return Ok(new
        {
            assetId = id,
            manualTags = updated!.Metadata.Tags,
            removed = tagName
        });
    }
}

public class AddTagRequest
{
    public string TagName { get; set; } = null!;
}
