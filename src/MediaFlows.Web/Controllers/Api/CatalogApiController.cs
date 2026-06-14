using MediaFlows.Shared.DTOs;
using MediaFlows.Shared.Interfaces;
using MediaFlows.Shared.Models.DynamoDb;
using MediaFlows.Shared.Models.Enums;
using MediaFlows.Shared.Models.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediaFlows.Web.Controllers.Api;

[Route("api/v1/catalog")]
[Authorize(Policy = "CanViewContent")]
public class CatalogApiController : ApiBaseController
{
    private const int TrendingFetchLimit = 50;

    private readonly IMediaAssetService _assetService;
    private readonly IDynamoDbService _dynamoDb;
    private readonly IBookmarkService _bookmarkService;
    private readonly ICommentService _commentService;
    private readonly IS3StorageService _s3Service;

    public CatalogApiController(
        IMediaAssetService assetService,
        IDynamoDbService dynamoDb,
        IBookmarkService bookmarkService,
        ICommentService commentService,
        IS3StorageService s3Service)
    {
        _assetService = assetService;
        _dynamoDb = dynamoDb;
        _bookmarkService = bookmarkService;
        _commentService = commentService;
        _s3Service = s3Service;
    }

    [HttpGet("")]
    public async Task<IActionResult> GetCatalog(int page = 1, string? type = null, string? sort = null)
    {
        if (string.Equals(sort, "trending", StringComparison.OrdinalIgnoreCase))
        {
            var trending = await FetchTrendingWithFallbackAsync();
            if (trending.Count == 0)
            {
                return Ok(new PagedResult<MediaAssetSummaryDto>
                {
                    Items = new List<MediaAssetSummaryDto>(),
                    TotalCount = 0,
                    Page = 1,
                    PageSize = TrendingFetchLimit,
                    HasMore = false
                });
            }

            var orderedIds = trending.Select(t => t.AssetId).ToList();
            var summaries = await _assetService.GetByIdsAsync(orderedIds, type);

            var byId = summaries.ToDictionary(s => s.Id);
            var ordered = orderedIds
                .Where(id => byId.ContainsKey(id))
                .Select(id => byId[id])
                .ToList();

            return Ok(new PagedResult<MediaAssetSummaryDto>
            {
                Items = ordered,
                TotalCount = ordered.Count,
                Page = 1,
                PageSize = TrendingFetchLimit,
                HasMore = false
            });
        }

        var result = await _assetService.GetPagedAssetsAsync(
            creatorId: null, status: AssetStatus.Published, page, pageSize: 20,
            fileType: type, sort: sort);

        return Ok(result);
    }

    private async Task<List<TrendingDataItem>> FetchTrendingWithFallbackAsync()
    {
        for (int daysBack = 1; daysBack <= 3; daysBack++)
        {
            var date = DateTime.UtcNow.AddDays(-daysBack).ToString("yyyy-MM-dd");
            var items = await _dynamoDb.GetTrendingAsync(date, TrendingFetchLimit);
            if (items.Count > 0) return items;
        }
        return new List<TrendingDataItem>();
    }

    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> Download(int id)
    {
        var asset = await _assetService.GetByIdAsync(id);
        if (asset == null || asset.Status != AssetStatus.Published)
            return ApiNotFound("Asset");

        var ext = asset.ContentType.Split('/').Last() switch
        {
            "jpeg" => ".jpg",
            "quicktime" => ".mov",
            "mpeg" => ".mp3",
            "svg+xml" => ".svg",
            "plain" => ".txt",
            var sub => $".{sub}"
        };
        var downloadFilename = Path.GetFileNameWithoutExtension(asset.Title) + ext;

        var presignedUrl = _s3Service.GeneratePresignedGetUrl(
            asset.S3Key, downloadFilename, TimeSpan.FromMinutes(5));

        return Redirect(presignedUrl);
    }

    [HttpGet("{id:int}/download-url")]
    public async Task<IActionResult> DownloadUrl(int id)
    {
        var asset = await _assetService.GetByIdAsync(id);
        if (asset == null || asset.Status != AssetStatus.Published)
            return ApiNotFound("Asset");

        var ext = asset.ContentType.Split('/').Last() switch
        {
            "jpeg" => ".jpg",
            "quicktime" => ".mov",
            "mpeg" => ".mp3",
            "svg+xml" => ".svg",
            "plain" => ".txt",
            var sub => $".{sub}"
        };
        var downloadFilename = Path.GetFileNameWithoutExtension(asset.Title) + ext;

        var presignedUrl = _s3Service.GeneratePresignedGetUrl(
            asset.S3Key, downloadFilename, TimeSpan.FromMinutes(5));

        return Ok(new { url = presignedUrl });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetAsset(int id)
    {
        var asset = await _assetService.GetByIdAsync(id);
        if (asset == null || asset.Status != AssetStatus.Published)
            return ApiNotFound("Asset");

        var mediaUrl = _s3Service.GetPublicUrl(asset.S3Key);

        int viewCount = 0;
        bool isBookmarked = false;
        object? comments = null;
        int commentCount = 0;
        object? relatedAssets = null;

        try { _ = _dynamoDb.IncrementViewCountAsync(id.ToString()); } catch { }
        try { viewCount = await _dynamoDb.GetViewCountAsync(id.ToString()); } catch { }
        try { isBookmarked = await _bookmarkService.IsBookmarkedAsync(CurrentUserId, id); } catch { }
        try { comments = await _commentService.GetCommentsAsync(id, CurrentUserId); } catch { }
        try { commentCount = await _commentService.GetCommentCountAsync(id); } catch { }
        try
        {
            var related = await _assetService.GetPagedAssetsAsync(
                creatorId: null, status: AssetStatus.Published, page: 1, pageSize: 6);
            relatedAssets = related.Items.Where(a => a.Id != id).Take(4).ToList();
        }
        catch { relatedAssets = new List<object>(); }

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
                asset.ScheduledPublishAt,
                asset.PublishedAt,
                asset.CreatedAt,
                asset.UpdatedAt
            },
            mediaUrl,
            viewCount,
            isBookmarked,
            comments = comments ?? new List<object>(),
            commentCount,
            relatedAssets = relatedAssets ?? new List<object>()
        });
    }
}
