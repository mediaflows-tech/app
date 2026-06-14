using MediaFlows.Data;
using MediaFlows.Shared.DTOs;
using MediaFlows.Shared.Interfaces;
using MediaFlows.Shared.Models.Entities;
using MediaFlows.Shared.Models.Enums;
using MediaFlows.Shared.Models.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MediaFlows.Web.Services;

public class MediaAssetService : IMediaAssetService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditLogService _auditLog;
    private readonly IS3StorageService _s3Storage;
    private readonly ILogger<MediaAssetService> _logger;

    public MediaAssetService(
        ApplicationDbContext db,
        IAuditLogService auditLog,
        IS3StorageService s3Storage,
        ILogger<MediaAssetService> logger)
    {
        _db = db;
        _auditLog = auditLog;
        _s3Storage = s3Storage;
        _logger = logger;
    }

    public async Task<PagedResult<MediaAssetSummaryDto>> GetPagedAssetsAsync(
        string? creatorId, AssetStatus? status, int page, int pageSize,
        string? fileType = null, string? sort = null)
    {
        var query = _db.MediaAssets.AsNoTracking()
            .Where(a => a.Status != AssetStatus.Deleted);

        if (!string.IsNullOrEmpty(creatorId))
            query = query.Where(a => a.CreatorId == creatorId);

        if (status.HasValue)
            query = query.Where(a => a.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(fileType))
            query = query.Where(a => a.ContentType.StartsWith(fileType));

        query = sort?.ToLowerInvariant() switch
        {
            "oldest" => query.OrderBy(a => a.CreatedAt),
            "name" => query.OrderBy(a => a.Title),
            "size" => query.OrderByDescending(a => a.FileSize),
            _ => query.OrderByDescending(a => a.CreatedAt)
        };

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                Summary = new MediaAssetSummaryDto
                {
                    Id = a.Id,
                    Title = a.Title,
                    ThumbnailUrl = a.ThumbnailUrl,
                    Status = a.Status,
                    ContentType = a.ContentType,
                    FileSize = a.FileSize,
                    CreatorName = a.Creator.DisplayName,
                    CreatedAt = a.CreatedAt,
                    PublishedAt = a.PublishedAt
                },
                a.S3Key
            })
            .ToListAsync();

        var summaries = items.Select(item =>
        {
            item.Summary.PreviewUrl = item.Summary.ThumbnailUrl;

            if (string.IsNullOrEmpty(item.Summary.PreviewUrl) &&
                item.Summary.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                item.Summary.PreviewUrl = _s3Storage.GetPublicUrl(item.S3Key);
            }

            if (string.IsNullOrEmpty(item.Summary.ThumbnailUrl) &&
                !string.IsNullOrEmpty(item.Summary.PreviewUrl))
            {
                item.Summary.ThumbnailUrl = item.Summary.PreviewUrl;
            }

            return item.Summary;
        }).ToList();

        return new PagedResult<MediaAssetSummaryDto>
        {
            Items = summaries,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            HasMore = (page * pageSize) < totalCount
        };
    }

    public async Task<List<MediaAssetSummaryDto>> GetByIdsAsync(
        IReadOnlyList<int> assetIds, string? fileType = null)
    {
        if (assetIds.Count == 0)
            return new List<MediaAssetSummaryDto>();

        // Materialize to List<int>: IReadOnlyList<T>.Contains is not reliably
        // translated to SQL IN by all EF Core providers (e.g. Npgsql).
        var ids = assetIds.ToList();

        var query = _db.MediaAssets.AsNoTracking()
            .Where(a => ids.Contains(a.Id))
            .Where(a => a.Status == AssetStatus.Published);

        if (!string.IsNullOrWhiteSpace(fileType))
            query = query.Where(a => a.ContentType.StartsWith(fileType));

        var items = await query
            .Select(a => new
            {
                Summary = new MediaAssetSummaryDto
                {
                    Id = a.Id,
                    Title = a.Title,
                    ThumbnailUrl = a.ThumbnailUrl,
                    Status = a.Status,
                    ContentType = a.ContentType,
                    FileSize = a.FileSize,
                    CreatorName = a.Creator.DisplayName,
                    CreatedAt = a.CreatedAt,
                    PublishedAt = a.PublishedAt
                },
                a.S3Key
            })
            .ToListAsync();

        return items.Select(item =>
        {
            item.Summary.PreviewUrl = item.Summary.ThumbnailUrl;

            if (string.IsNullOrEmpty(item.Summary.PreviewUrl) &&
                item.Summary.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                item.Summary.PreviewUrl = _s3Storage.GetPublicUrl(item.S3Key);
            }

            if (string.IsNullOrEmpty(item.Summary.ThumbnailUrl) &&
                !string.IsNullOrEmpty(item.Summary.PreviewUrl))
            {
                item.Summary.ThumbnailUrl = item.Summary.PreviewUrl;
            }

            return item.Summary;
        }).ToList();
    }

    public async Task<MediaAsset?> GetByIdAsync(int id)
    {
        return await _db.MediaAssets
            .Include(a => a.Creator)
            .Include(a => a.Versions.OrderByDescending(v => v.VersionNumber))
            .Include(a => a.Reviews.OrderByDescending(r => r.ReviewedAt))
            .AsSplitQuery()
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<MediaAsset> CreateAsync(MediaAsset asset)
    {
        _db.MediaAssets.Add(asset);
        await _db.SaveChangesAsync();

        var initialVersion = new AssetVersion
        {
            AssetId = asset.Id,
            VersionNumber = 1,
            S3Key = asset.S3Key,
            ContentType = asset.ContentType,
            FileSize = asset.FileSize,
            UploadedById = asset.CreatorId,
            ChangeNotes = "Initial upload"
        };
        _db.AssetVersions.Add(initialVersion);
        await _db.SaveChangesAsync();

        asset.CurrentVersionId = initialVersion.Id;
        await _db.SaveChangesAsync();

        await _auditLog.LogAsync("MediaAsset.Create", "MediaAsset", asset.Id.ToString(),
            new { asset.Title, asset.ContentType, asset.CreatorId });

        return asset;
    }

    public async Task UpdateMetadataAsync(int assetId, MediaMetadata metadata)
    {
        var asset = await _db.MediaAssets.FindAsync(assetId);
        if (asset == null) throw new KeyNotFoundException($"Asset {assetId} not found");

        asset.Metadata = metadata;
        await _db.SaveChangesAsync();
    }

    public async Task UpdateStatusAsync(int assetId, AssetStatus newStatus, string updatedBy)
    {
        await _db.MediaAssets
            .Where(a => a.Id == assetId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.Status, newStatus)
                .SetProperty(a => a.UpdatedAt, DateTime.UtcNow));

        await _auditLog.LogAsync("MediaAsset.StatusChange", "MediaAsset", assetId.ToString(),
            new { NewStatus = newStatus.ToString(), UpdatedBy = updatedBy });
    }

    public async Task AddTagAsync(int assetId, string tagName, float confidence = 1.0f)
    {
        var asset = await _db.MediaAssets.FindAsync(assetId);
        if (asset == null) throw new KeyNotFoundException($"Asset {assetId} not found");

        asset.Metadata.Tags ??= new List<string>();
        var normalized = tagName.Trim().ToLower();
        if (!asset.Metadata.Tags.Any(t => t.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
        {
            asset.Metadata.Tags.Add(normalized);
            await _db.SaveChangesAsync();
        }
    }

    public async Task RemoveTagAsync(int assetId, string tagName)
    {
        var asset = await _db.MediaAssets.FindAsync(assetId);
        if (asset == null) throw new KeyNotFoundException($"Asset {assetId} not found");

        asset.Metadata.Tags ??= new List<string>();
        var existing = asset.Metadata.Tags
            .FirstOrDefault(t => t.Equals(tagName, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            asset.Metadata.Tags.Remove(existing);
            await _db.SaveChangesAsync();
        }
    }

    public async Task UpdateTitleAsync(int assetId, string newTitle)
    {
        await _db.MediaAssets
            .Where(a => a.Id == assetId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.Title, newTitle)
                .SetProperty(a => a.UpdatedAt, DateTime.UtcNow));
    }

    public async Task UpdateDescriptionAsync(int assetId, string? newDescription)
    {
        await _db.MediaAssets
            .Where(a => a.Id == assetId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.Description, newDescription)
                .SetProperty(a => a.UpdatedAt, DateTime.UtcNow));
    }

    public async Task DeleteAsync(int assetId, string deletedBy)
    {
        var asset = await _db.MediaAssets
            .Include(a => a.Comments)
            .Include(a => a.Reviews)
            .Include(a => a.Bookmarks)
            .Include(a => a.Versions)
            .AsSplitQuery()
            .FirstOrDefaultAsync(a => a.Id == assetId);
        if (asset == null) throw new KeyNotFoundException($"Asset {assetId} not found");

        // The asset is soft-deleted, so the FK cascade on these dependents
        // never fires. Remove them explicitly, or they dangle behind the
        // hidden asset (e.g. bookmarks pointing at a null navigation).
        _db.Comments.RemoveRange(asset.Comments);
        _db.Reviews.RemoveRange(asset.Reviews);
        _db.Bookmarks.RemoveRange(asset.Bookmarks);
        asset.CurrentVersionId = null; // references a version being removed
        _db.AssetVersions.RemoveRange(asset.Versions);

        asset.IsDeleted = true;
        asset.Status = AssetStatus.Deleted;
        asset.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _auditLog.LogAsync("MediaAsset.Delete", "MediaAsset", assetId.ToString(),
            new { DeletedBy = deletedBy });
    }
}
