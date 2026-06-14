using MediaFlows.Data;
using MediaFlows.Shared.DTOs;
using MediaFlows.Shared.Interfaces;
using MediaFlows.Shared.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MediaFlows.Web.Services;

public class BookmarkService : IBookmarkService
{
    private readonly ApplicationDbContext _db;
    private readonly IS3StorageService? _s3Storage;

    public BookmarkService(ApplicationDbContext db, IS3StorageService s3Storage)
    {
        _db = db;
        _s3Storage = s3Storage;
    }

    public BookmarkService(ApplicationDbContext db) => _db = db;

    public async Task<bool> ToggleBookmarkAsync(string userId, int assetId)
    {
        var existing = await _db.Bookmarks
            .FirstOrDefaultAsync(b => b.UserId == userId && b.AssetId == assetId);

        if (existing != null)
        {
            _db.Bookmarks.Remove(existing);
            await _db.SaveChangesAsync();
            return false; // Removed
        }

        _db.Bookmarks.Add(new Bookmark
        {
            UserId = userId,
            AssetId = assetId,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return true; // Added
    }

    public async Task<bool> IsBookmarkedAsync(string userId, int assetId)
    {
        return await _db.Bookmarks
            .AnyAsync(b => b.UserId == userId && b.AssetId == assetId);
    }

    public async Task<PagedResult<MediaAssetSummaryDto>> GetUserBookmarksAsync(
        string userId, int page, int pageSize)
    {
        var query = _db.Bookmarks
            .Where(b => b.UserId == userId)
            .Include(b => b.Asset)
                .ThenInclude(a => a.Creator)
            .OrderByDescending(b => b.CreatedAt);

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new
            {
                Summary = new MediaAssetSummaryDto
                {
                    Id = b.Asset.Id,
                    Title = b.Asset.Title,
                    ThumbnailUrl = b.Asset.ThumbnailUrl,
                    Status = b.Asset.Status,
                    ContentType = b.Asset.ContentType,
                    FileSize = b.Asset.FileSize,
                    CreatorName = b.Asset.Creator.DisplayName,
                    CreatedAt = b.Asset.CreatedAt,
                    PublishedAt = b.Asset.PublishedAt
                },
                b.Asset.S3Key
            })
            .AsNoTracking()
            .ToListAsync();

        var summaries = items.Select(item =>
        {
            item.Summary.PreviewUrl = item.Summary.ThumbnailUrl;

            if (string.IsNullOrEmpty(item.Summary.PreviewUrl) &&
                item.Summary.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) &&
                _s3Storage != null)
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

    public async Task<int> GetBookmarkCountAsync(string userId)
    {
        return await _db.Bookmarks.CountAsync(b => b.UserId == userId);
    }
}
