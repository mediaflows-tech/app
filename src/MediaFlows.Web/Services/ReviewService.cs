using MediaFlows.Data;
using MediaFlows.Shared.DTOs;
using MediaFlows.Shared.Interfaces;
using MediaFlows.Shared.Models.Entities;
using MediaFlows.Shared.Models.Enums;
using MediaFlows.Web.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace MediaFlows.Web.Services;

public class ReviewService : IReviewService
{
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<NotificationHub, INotificationClient> _notificationHub;
    private readonly INotificationService _notificationService;
    private readonly IAuditLogService _auditLog;
    private readonly IS3StorageService _s3Storage;
    private readonly IReviewEventPublisher _reviewEventPublisher;

    public ReviewService(
        ApplicationDbContext db,
        IHubContext<NotificationHub, INotificationClient> notificationHub,
        INotificationService notificationService,
        IAuditLogService auditLog,
        IS3StorageService s3Storage,
        IReviewEventPublisher reviewEventPublisher)
    {
        _db = db;
        _notificationHub = notificationHub;
        _notificationService = notificationService;
        _auditLog = auditLog;
        _s3Storage = s3Storage;
        _reviewEventPublisher = reviewEventPublisher;
    }

    public async Task<PagedResult<ReviewListItemDto>> GetPendingReviewsAsync(
        AssetStatus? status, string? creatorId, string? contentType,
        string? sortBy, string? sortDir, int page, int pageSize)
    {
        var query = _db.MediaAssets
            .Include(a => a.Creator)
            .AsNoTracking();

        if (status.HasValue)
            query = query.Where(a => a.Status == status.Value);
        else
            query = query.Where(a => a.Status != AssetStatus.Draft &&
                                     a.Status != AssetStatus.Archived &&
                                     a.Status != AssetStatus.Quarantined &&
                                     a.Status != AssetStatus.Deleted);

        if (!string.IsNullOrEmpty(creatorId))
            query = query.Where(a => a.CreatorId == creatorId);

        if (!string.IsNullOrEmpty(contentType))
            query = query.Where(a => a.ContentType.StartsWith(contentType));

        query = (sortBy?.ToLower(), sortDir?.ToLower()) switch
        {
            ("title", "asc") => query.OrderBy(a => a.Title),
            ("title", _) => query.OrderByDescending(a => a.Title),
            ("creator", "asc") => query.OrderBy(a => a.Creator.DisplayName),
            ("creator", _) => query.OrderByDescending(a => a.Creator.DisplayName),
            ("status", "asc") => query.OrderBy(a => a.Status),
            ("status", _) => query.OrderByDescending(a => a.Status),
            ("size", "asc") => query.OrderBy(a => a.FileSize),
            ("size", _) => query.OrderByDescending(a => a.FileSize),
            ("date", "asc") => query.OrderBy(a => a.CreatedAt),
            _ => query.OrderByDescending(a => a.CreatedAt)
        };

        var totalCount = await query.CountAsync();

        var rawItems = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                Dto = new ReviewListItemDto
                {
                    AssetId = a.Id,
                    Title = a.Title,
                    ThumbnailUrl = a.ThumbnailUrl,
                    ContentType = a.ContentType,
                    CreatorId = a.CreatorId,
                    CreatorName = a.Creator.Email,
                    Status = a.Status,
                    CreatedAt = a.CreatedAt,
                    FileSize = a.FileSize,
                    ReviewCount = a.Reviews.Count,
                    LastReviewedAt = a.Reviews
                        .OrderByDescending(r => r.ReviewedAt)
                        .Select(r => (DateTime?)r.ReviewedAt)
                        .FirstOrDefault()
                },
                a.S3Key
            })
            .ToListAsync();

        var items = rawItems.Select(item =>
        {
            if (string.IsNullOrEmpty(item.Dto.ThumbnailUrl) &&
                item.Dto.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                item.Dto.ThumbnailUrl = _s3Storage.GetPublicUrl(item.S3Key);
            }

            return item.Dto;
        }).ToList();

        return new PagedResult<ReviewListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            HasMore = (page * pageSize) < totalCount
        };
    }

    public async Task<ReviewDetailsDto?> GetReviewDetailsAsync(int assetId)
    {
        return await _db.MediaAssets
            .Where(a => a.Id == assetId)
            .Select(a => new ReviewDetailsDto
            {
                AssetId = a.Id,
                Title = a.Title,
                Description = a.Description,
                ThumbnailUrl = a.ThumbnailUrl,
                ContentType = a.ContentType,
                S3Key = a.S3Key,
                CreatorId = a.CreatorId,
                CreatorName = a.Creator.Email,
                Status = a.Status,
                CreatedAt = a.CreatedAt,
                FileSize = a.FileSize,
                ScheduledPublishAt = a.ScheduledPublishAt,
                ReviewHistory = a.Reviews
                    .OrderBy(r => r.ReviewedAt)
                    .Select(r => new ReviewHistoryItemDto
                    {
                        ReviewerName = r.Reviewer.DisplayName,
                        Decision = r.Decision,
                        Comments = r.Comments,
                        ReviewedAt = r.ReviewedAt
                    })
                    .ToList()
            })
            .AsNoTracking()
            .FirstOrDefaultAsync();
    }

    public async Task SubmitDecisionAsync(int assetId, ReviewDecision decision,
        string reviewerId, string? comments)
    {
        var asset = await _db.MediaAssets.FindAsync(assetId)
            ?? throw new InvalidOperationException($"Asset {assetId} not found");

        if (asset.Status != AssetStatus.PendingReview && asset.Status != AssetStatus.Submitted)
            throw new InvalidOperationException(
                $"Asset must be PendingReview or Submitted to review. Current status: {asset.Status}");

        if (decision == ReviewDecision.Rejected && string.IsNullOrWhiteSpace(comments))
            throw new ArgumentException("Comments are required when rejecting an asset.");

        if (decision == ReviewDecision.ChangesRequested && string.IsNullOrWhiteSpace(comments))
            throw new ArgumentException("Comments are required when requesting changes.");

        var review = new Review
        {
            AssetId = assetId,
            ReviewerId = reviewerId,
            Decision = decision,
            Comments = comments,
            ReviewedAt = DateTime.UtcNow
        };
        _db.Reviews.Add(review);

        var newStatus = GetStatusFromDecision(decision);
        if (newStatus.HasValue)
            asset.Status = newStatus.Value;

        await _db.SaveChangesAsync();

        await _auditLog.LogAsync(
            "Review.Decision",
            "MediaAsset",
            assetId.ToString(),
            new { Decision = decision.ToString(), ReviewerId = reviewerId, Comments = comments });

        var notificationTitle = $"Review Decision: {decision}";
        var notificationMessage = $"Your asset \"{asset.Title}\" has been {decision.ToString().ToLower()}.";
        await NotifyCreatorAsync(
            asset.CreatorId,
            notificationTitle,
            notificationMessage,
            decision == ReviewDecision.Approved ? "success" : "warning");

        await BroadcastPendingCountAsync();
    }

    public async Task BatchDecisionAsync(int[] assetIds, ReviewDecision decision,
        string reviewerId, string? comments)
    {
        var assets = await _db.MediaAssets
            .Where(a => assetIds.Contains(a.Id) &&
                        (a.Status == AssetStatus.PendingReview || a.Status == AssetStatus.Submitted))
            .ToListAsync();

        if (assets.Count == 0) return;

        var newStatus = GetStatusFromDecision(decision);

        foreach (var asset in assets)
        {
            _db.Reviews.Add(new Review
            {
                AssetId = asset.Id,
                ReviewerId = reviewerId,
                Decision = decision,
                Comments = comments,
                ReviewedAt = DateTime.UtcNow
            });

            if (newStatus.HasValue)
                asset.Status = newStatus.Value;
        }

        await _db.SaveChangesAsync();

        foreach (var asset in assets)
        {
            await _auditLog.LogAsync(
                "Review.Decision",
                "MediaAsset",
                asset.Id.ToString(),
                new { Decision = decision.ToString(), ReviewerId = reviewerId, Comments = comments });

            var notificationTitle = $"Review Decision: {decision}";
            var notificationMessage = $"Your asset \"{asset.Title}\" has been {decision.ToString().ToLower()}.";
            await NotifyCreatorAsync(
                asset.CreatorId,
                notificationTitle,
                notificationMessage,
                decision == ReviewDecision.Approved ? "success" : "warning");
        }

        await BroadcastPendingCountAsync();
    }

    public async Task ApproveAndPublishAsync(int assetId, string reviewerId, string? comments)
    {
        var asset = await _db.MediaAssets.FindAsync(assetId)
            ?? throw new InvalidOperationException($"Asset {assetId} not found");

        if (asset.Status != AssetStatus.PendingReview && asset.Status != AssetStatus.Submitted)
            throw new InvalidOperationException(
                $"Asset must be PendingReview or Submitted. Current status: {asset.Status}");

        var now = DateTime.UtcNow;

        _db.Reviews.Add(new Review
        {
            AssetId = assetId,
            ReviewerId = reviewerId,
            Decision = ReviewDecision.Approved,
            Comments = comments,
            ReviewedAt = now
        });

        asset.Status = AssetStatus.Published;
        asset.PublishedAt = now;
        asset.ScheduledPublishAt = null;

        await _db.SaveChangesAsync();

        await _auditLog.LogAsync(
            "Review.ApproveAndPublish",
            "MediaAsset",
            assetId.ToString(),
            new { ReviewerId = reviewerId, PublishedAt = now.ToString("O") });

        await NotifyCreatorAsync(
            asset.CreatorId,
            "Asset Published",
            $"Your asset \"{asset.Title}\" has been approved and published.",
            "success");

        await BroadcastPendingCountAsync();
    }

    public async Task ApproveAndScheduleAsync(int assetId, DateTime scheduledAt, string reviewerId, string? comments)
    {
        scheduledAt = NormalizeToUtc(scheduledAt);

        var asset = await _db.MediaAssets.FindAsync(assetId)
            ?? throw new InvalidOperationException($"Asset {assetId} not found");

        if (asset.Status != AssetStatus.PendingReview && asset.Status != AssetStatus.Submitted)
            throw new InvalidOperationException(
                $"Asset must be PendingReview or Submitted. Current status: {asset.Status}");

        if (scheduledAt <= DateTime.UtcNow)
            throw new ArgumentException("Scheduled publish date must be in the future.");

        var now = DateTime.UtcNow;

        _db.Reviews.Add(new Review
        {
            AssetId = assetId,
            ReviewerId = reviewerId,
            Decision = ReviewDecision.Approved,
            Comments = comments,
            ReviewedAt = now
        });

        asset.Status = AssetStatus.Approved;
        asset.ScheduledPublishAt = scheduledAt;

        await _db.SaveChangesAsync();

        await _auditLog.LogAsync(
            "Review.ApproveAndSchedule",
            "MediaAsset",
            assetId.ToString(),
            new { ReviewerId = reviewerId, ScheduledAt = scheduledAt.ToString("O") });

        await NotifyCreatorAsync(
            asset.CreatorId,
            "Asset Approved & Scheduled",
            $"Your asset \"{asset.Title}\" has been approved and scheduled for {scheduledAt:MMM d, yyyy h:mm tt} UTC.",
            "success");

        await BroadcastPendingCountAsync();
    }

    public async Task<int> BatchApproveAndPublishAsync(int[] assetIds, string reviewerId, string? comments)
    {
        var now = DateTime.UtcNow;

        var validStatuses = new[]
        {
            AssetStatus.PendingReview, AssetStatus.Submitted, AssetStatus.Approved
        };
        var assets = await _db.MediaAssets
            .Where(a => assetIds.Contains(a.Id) && validStatuses.Contains(a.Status))
            .ToListAsync();

        if (assets.Count == 0) return 0;

        foreach (var asset in assets)
        {
            // Only emit a new Review row when this is the approval step.
            // Already-Approved assets just transition Approved → Published.
            if (asset.Status is AssetStatus.PendingReview or AssetStatus.Submitted)
            {
                _db.Reviews.Add(new Review
                {
                    AssetId = asset.Id,
                    ReviewerId = reviewerId,
                    Decision = ReviewDecision.Approved,
                    Comments = comments,
                    ReviewedAt = now
                });
            }

            asset.Status = AssetStatus.Published;
            asset.PublishedAt = now;
            asset.ScheduledPublishAt = null;
        }

        await _db.SaveChangesAsync();

        foreach (var asset in assets)
        {
            await _auditLog.LogAsync(
                "Review.ApproveAndPublish",
                "MediaAsset",
                asset.Id.ToString(),
                new { ReviewerId = reviewerId, PublishedAt = now.ToString("O") });

            await NotifyCreatorAsync(
                asset.CreatorId,
                "Asset Published",
                $"Your asset \"{asset.Title}\" has been approved and published.",
                "success");
        }

        await BroadcastPendingCountAsync();
        return assets.Count;
    }

    public async Task<int> GetPendingCountAsync()
    {
        return await _db.MediaAssets
            .CountAsync(a => a.Status == AssetStatus.PendingReview || a.Status == AssetStatus.Submitted);
    }

    public async Task<int> GetApprovedCountAsync()
    {
        return await _db.MediaAssets.CountAsync(a => a.Status == AssetStatus.Approved);
    }

    public async Task<int> GetRejectedCountAsync()
    {
        return await _db.MediaAssets.CountAsync(a => a.Status == AssetStatus.Rejected);
    }

    private static readonly TimeZoneInfo AppTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Singapore");

    private static DateTime NormalizeToUtc(DateTime dt) =>
        dt.Kind switch
        {
            DateTimeKind.Utc => dt,
            DateTimeKind.Local => dt.ToUniversalTime(),
            _ => TimeZoneInfo.ConvertTimeToUtc(dt, AppTimeZone)
        };

    private static AssetStatus? GetStatusFromDecision(ReviewDecision decision) => decision switch
    {
        ReviewDecision.Approved => AssetStatus.Approved,
        ReviewDecision.Rejected => AssetStatus.Rejected,
        ReviewDecision.ChangesRequested => AssetStatus.ChangesRequested,
        _ => null
    };

    private async Task NotifyCreatorAsync(
        string creatorId,
        string title,
        string message,
        string toastType)
    {
        await _notificationService.CreateNotificationAsync(
            creatorId, title, message, NotificationType.ReviewDecision);

        await _notificationHub.Clients.Group($"user_{creatorId}")
            .ReceiveToast(title, message, toastType);

        // Emit a domain event so the NotificationDispatcher Lambda delivers the
        // out-of-band (email) notification. Best-effort — see ReviewEventPublisher.
        await _reviewEventPublisher.PublishCreatorNotificationAsync(creatorId, title, message);
    }

    private async Task BroadcastPendingCountAsync()
    {
        var pendingCount = await GetPendingCountAsync();
        await _notificationHub.Clients.Groups("role_Editor", "role_SystemAdmin")
            .UpdateBadge("pending-review-count", pendingCount.ToString());
    }

    public async Task<List<ScheduledPublishDto>> GetScheduledAssetsAsync(DateTime start, DateTime end)
    {
        return await _db.MediaAssets
            .Where(a => a.Status == AssetStatus.Approved
                     && a.ScheduledPublishAt != null
                     && a.ScheduledPublishAt >= start
                     && a.ScheduledPublishAt <= end)
            .Select(a => new ScheduledPublishDto
            {
                AssetId = a.Id,
                Title = a.Title,
                ThumbnailUrl = a.ThumbnailUrl,
                ScheduledPublishAt = a.ScheduledPublishAt,
                Status = a.Status
            })
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task SchedulePublishAsync(int assetId, DateTime scheduledAt, string editorId)
    {
        scheduledAt = NormalizeToUtc(scheduledAt);

        var asset = await _db.MediaAssets.FindAsync(assetId)
            ?? throw new InvalidOperationException($"Asset {assetId} not found");

        if (asset.Status != AssetStatus.Approved)
            throw new InvalidOperationException("Only approved assets can be scheduled for publishing.");

        if (asset.ScheduledPublishAt.HasValue)
            throw new InvalidOperationException("Asset is already scheduled. Use reschedule to change the date.");

        if (scheduledAt <= DateTime.UtcNow)
            throw new ArgumentException("Scheduled publish date must be in the future.");

        asset.ScheduledPublishAt = scheduledAt;
        await _db.SaveChangesAsync();

        await _auditLog.LogAsync(
            "Schedule.Created",
            "MediaAsset",
            assetId.ToString(),
            new { ScheduledAt = scheduledAt, EditorId = editorId });
    }

    public async Task ReschedulePublishAsync(int assetId, DateTime newScheduledAt, string editorId)
    {
        newScheduledAt = NormalizeToUtc(newScheduledAt);

        var asset = await _db.MediaAssets.FindAsync(assetId)
            ?? throw new InvalidOperationException($"Asset {assetId} not found");

        if (asset.Status != AssetStatus.Approved)
            throw new InvalidOperationException("Only approved assets can be rescheduled.");

        if (!asset.ScheduledPublishAt.HasValue)
            throw new InvalidOperationException("Asset is not scheduled. Use SchedulePublishAsync to set a schedule.");

        if (newScheduledAt <= DateTime.UtcNow)
            throw new ArgumentException("Scheduled publish date must be in the future.");

        var oldDate = asset.ScheduledPublishAt;
        asset.ScheduledPublishAt = newScheduledAt;
        await _db.SaveChangesAsync();

        await _auditLog.LogAsync(
            "Schedule.Updated",
            "MediaAsset",
            assetId.ToString(),
            new { OldDate = oldDate, NewDate = newScheduledAt, EditorId = editorId });
    }

    public async Task UnscheduleAsync(int assetId, string editorId)
    {
        var asset = await _db.MediaAssets.FindAsync(assetId)
            ?? throw new InvalidOperationException($"Asset {assetId} not found");

        if (asset.Status != AssetStatus.Approved)
            throw new InvalidOperationException("Only approved assets can be unscheduled.");

        if (!asset.ScheduledPublishAt.HasValue)
            throw new InvalidOperationException("Asset is not scheduled.");

        var oldDate = asset.ScheduledPublishAt;
        asset.ScheduledPublishAt = null;
        await _db.SaveChangesAsync();

        await _auditLog.LogAsync(
            "Schedule.Cancelled",
            "MediaAsset",
            assetId.ToString(),
            new { OldDate = oldDate, EditorId = editorId });
    }

    public async Task BatchScheduleAsync(int[] assetIds, DateTime scheduledAt, string editorId)
    {
        scheduledAt = NormalizeToUtc(scheduledAt);

        if (scheduledAt <= DateTime.UtcNow)
            throw new ArgumentException("Scheduled publish date must be in the future.");

        var assets = await _db.MediaAssets
            .Where(a => assetIds.Contains(a.Id))
            .ToListAsync();

        var errors = new List<string>();
        foreach (var asset in assets)
        {
            if (asset.Status != AssetStatus.Approved)
                errors.Add($"\"{asset.Title}\" is not approved");
            else if (asset.ScheduledPublishAt.HasValue)
                errors.Add($"\"{asset.Title}\" is already scheduled");
            else
                asset.ScheduledPublishAt = scheduledAt;
        }

        var scheduledCount = assets.Count - errors.Count;

        if (scheduledCount == 0)
            throw new InvalidOperationException(string.Join("; ", errors));

        await _db.SaveChangesAsync();

        var scheduledIds = assets
            .Where(a => a.ScheduledPublishAt == scheduledAt)
            .Select(a => a.Id);

        await _auditLog.LogAsync(
            "Schedule.BatchCreated",
            "MediaAsset",
            string.Join(",", scheduledIds),
            new { ScheduledAt = scheduledAt, EditorId = editorId, Count = scheduledCount, Skipped = errors.Count });

        if (errors.Count > 0)
            throw new InvalidOperationException(
                $"Scheduled {scheduledCount} asset(s). Skipped: {string.Join("; ", errors)}");
    }

    public async Task BatchApproveAndScheduleAsync(int[] assetIds, DateTime scheduledAt, string reviewerId)
    {
        scheduledAt = NormalizeToUtc(scheduledAt);

        if (scheduledAt <= DateTime.UtcNow)
            throw new ArgumentException("Scheduled publish date must be in the future.");

        var validStatuses = new[] { AssetStatus.PendingReview, AssetStatus.Submitted, AssetStatus.Approved };
        var assets = await _db.MediaAssets
            .Where(a => assetIds.Contains(a.Id) && validStatuses.Contains(a.Status))
            .ToListAsync();

        if (assets.Count == 0)
            throw new InvalidOperationException("No eligible assets found.");

        var now = DateTime.UtcNow;
        var errors = new List<string>();
        foreach (var asset in assets)
        {
            if (asset.Status == AssetStatus.Approved && asset.ScheduledPublishAt.HasValue)
            {
                errors.Add($"\"{asset.Title}\" is already scheduled");
                continue;
            }
            if (asset.Status != AssetStatus.Approved)
            {
                _db.Reviews.Add(new Review
                {
                    AssetId = asset.Id,
                    ReviewerId = reviewerId,
                    Decision = ReviewDecision.Approved,
                    Comments = "Approved via batch schedule",
                    ReviewedAt = now
                });
                asset.Status = AssetStatus.Approved;
            }
            asset.ScheduledPublishAt = scheduledAt;
        }

        var scheduledCount = assets.Count - errors.Count;
        if (scheduledCount == 0)
            throw new InvalidOperationException(
                errors.Count > 0 ? string.Join("; ", errors) : "No eligible assets found.");

        await _db.SaveChangesAsync();

        var scheduledIds = assets.Where(a => a.ScheduledPublishAt == scheduledAt).Select(a => a.Id);
        await _auditLog.LogAsync(
            "Review.BatchApproveAndSchedule",
            "MediaAsset",
            string.Join(",", scheduledIds),
            new { ScheduledAt = scheduledAt, ReviewerId = reviewerId, Count = scheduledCount });

        foreach (var asset in assets.Where(a => a.ScheduledPublishAt == scheduledAt))
        {
            var title = "Asset Approved & Scheduled";
            var msg = $"Your asset \"{asset.Title}\" has been approved and scheduled for {scheduledAt:MMM d, yyyy h:mm tt} UTC.";
            await NotifyCreatorAsync(asset.CreatorId, title, msg, "success");
        }

        await BroadcastPendingCountAsync();

        if (errors.Count > 0)
            throw new InvalidOperationException(
                $"Scheduled {scheduledCount} asset(s). Skipped: {string.Join("; ", errors)}");
    }

    public async Task PublishNowAsync(int assetId, string editorId)
    {
        var now = DateTime.UtcNow;

        // Atomic update with WHERE guard — prevents race with ScheduledPublisherWorker
        var updated = await _db.MediaAssets
            .Where(a => a.Id == assetId && a.Status == AssetStatus.Approved)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.Status, AssetStatus.Published)
                .SetProperty(a => a.PublishedAt, now)
                .SetProperty(a => a.UpdatedAt, now)
                .SetProperty(a => a.ScheduledPublishAt, (DateTime?)null));

        if (updated == 0)
        {
            var asset = await _db.MediaAssets.FindAsync(assetId);
            if (asset is null)
                throw new InvalidOperationException($"Asset {assetId} not found");
            if (asset.Status == AssetStatus.Published)
                return; // Already published by worker — treat as success
            throw new InvalidOperationException(
                $"Asset must be Approved to publish. Current status: {asset.Status}");
        }

        var published = await _db.MediaAssets
            .Where(a => a.Id == assetId)
            .Select(a => new { a.Title, a.CreatorId })
            .FirstAsync();

        await _auditLog.LogAsync(
            "Review.PublishNow",
            "MediaAsset",
            assetId.ToString(),
            new { EditorId = editorId, PublishedAt = now.ToString("O") });

        await NotifyCreatorAsync(
            published.CreatorId,
            "Asset Published",
            $"Your asset \"{published.Title}\" has been published.",
            "success");
    }

    public async Task<int> PublishDueScheduledAsync()
    {
        var now = DateTime.UtcNow;

        var dueAssets = await _db.MediaAssets
            .Where(a => a.Status == AssetStatus.Approved
                     && a.ScheduledPublishAt != null
                     && a.ScheduledPublishAt <= now)
            .Select(a => new { a.Id, a.Title, a.CreatorId })
            .ToListAsync();

        var publishedCount = 0;

        foreach (var asset in dueAssets)
        {
            // Atomic, guarded transition — only publishes a row still in Approved
            // state, so a concurrent PublishNowAsync (or another instance's worker)
            // cannot cause a double-publish or a duplicate notification.
            var updated = await _db.MediaAssets
                .Where(a => a.Id == asset.Id && a.Status == AssetStatus.Approved)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(a => a.Status, AssetStatus.Published)
                    .SetProperty(a => a.PublishedAt, now)
                    .SetProperty(a => a.UpdatedAt, now)
                    .SetProperty(a => a.ScheduledPublishAt, (DateTime?)null));

            if (updated == 0)
                continue;

            publishedCount++;

            await _auditLog.LogAsync(
                "Schedule.Published",
                "MediaAsset",
                asset.Id.ToString(),
                new { PublishedAt = now.ToString("O") });

            await NotifyCreatorAsync(
                asset.CreatorId,
                "Asset Published",
                $"Your scheduled asset \"{asset.Title}\" is now live.",
                "success");
        }

        return publishedCount;
    }

    public async Task RejectApprovedAsync(int assetId, string reviewerId, string comments)
    {
        var asset = await _db.MediaAssets.FindAsync(assetId)
            ?? throw new InvalidOperationException($"Asset {assetId} not found");

        if (asset.Status != AssetStatus.Approved)
            throw new InvalidOperationException(
                $"Asset must be Approved to reject. Current status: {asset.Status}");

        _db.Reviews.Add(new Review
        {
            AssetId = assetId,
            ReviewerId = reviewerId,
            Decision = ReviewDecision.Rejected,
            Comments = comments,
            ReviewedAt = DateTime.UtcNow
        });

        asset.Status = AssetStatus.Rejected;
        asset.ScheduledPublishAt = null;

        await _db.SaveChangesAsync();

        await _auditLog.LogAsync(
            "Review.RejectApproved",
            "MediaAsset",
            assetId.ToString(),
            new { ReviewerId = reviewerId, Comments = comments });

        await NotifyCreatorAsync(
            asset.CreatorId,
            "Asset Rejected",
            $"Your asset \"{asset.Title}\" has been rejected.",
            "warning");

        await BroadcastPendingCountAsync();
    }

    public async Task<List<ScheduledPublishDto>> GetAvailableAssetsAsync()
    {
        return await _db.MediaAssets
            .Where(a => a.Status == AssetStatus.Approved && a.ScheduledPublishAt == null)
            .Select(a => new ScheduledPublishDto
            {
                AssetId = a.Id,
                Title = a.Title,
                ThumbnailUrl = a.ThumbnailUrl,
                Status = a.Status
            })
            .OrderBy(a => a.Title)
            .Take(200)
            .AsNoTracking()
            .ToListAsync();
    }
}
