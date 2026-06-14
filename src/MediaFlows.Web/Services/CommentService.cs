using MediaFlows.Data;
using MediaFlows.Shared.DTOs;
using MediaFlows.Shared.Interfaces;
using MediaFlows.Shared.Models.Entities;
using MediaFlows.Web.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace MediaFlows.Web.Services;

public class CommentService : ICommentService
{
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<NotificationHub, INotificationClient> _hub;

    public CommentService(
        ApplicationDbContext db,
        IHubContext<NotificationHub, INotificationClient> hub)
    {
        _db = db;
        _hub = hub;
    }

    public async Task<CommentDto> AddCommentAsync(int assetId, string authorId, string content, int? parentCommentId = null)
    {
        if (parentCommentId.HasValue)
        {
            var parent = await _db.Comments.FindAsync(parentCommentId.Value);
            if (parent == null || parent.AssetId != assetId)
                throw new InvalidOperationException("Parent comment not found or belongs to a different asset.");
        }

        var comment = new Comment
        {
            AssetId = assetId,
            AuthorId = authorId,
            Content = content,
            ParentCommentId = parentCommentId,
            CreatedAt = DateTime.UtcNow
        };

        _db.Comments.Add(comment);
        await _db.SaveChangesAsync();

        var user = await _db.AppUsers.FirstOrDefaultAsync(u => u.CognitoSub == authorId);

        var dto = new CommentDto
        {
            Id = comment.Id,
            AssetId = comment.AssetId,
            AuthorId = comment.AuthorId,
            AuthorDisplayName = user?.DisplayName ?? "Unknown",
            Content = comment.Content,
            ParentCommentId = comment.ParentCommentId,
            CreatedAt = comment.CreatedAt,
            IsOwner = true,
            Replies = new()
        };

        var asset = await _db.MediaAssets.FindAsync(assetId);
        if (asset != null && asset.CreatorId != authorId)
        {
            await _hub.Clients.Group($"user_{asset.CreatorId}")
                .ReceiveToast(
                    "New Comment",
                    $"Someone commented on \"{asset.Title}\"",
                    "info");
        }

        if (parentCommentId.HasValue)
        {
            var parentComment = await _db.Comments.FindAsync(parentCommentId.Value);
            if (parentComment != null && parentComment.AuthorId != authorId)
            {
                await _hub.Clients.Group($"user_{parentComment.AuthorId}")
                    .ReceiveToast(
                        "New Reply",
                        $"Someone replied to your comment on \"{asset?.Title}\"",
                        "info");
            }
        }

        await _hub.Clients.Group($"asset_{assetId}")
            .ReceiveNotification($"<div id='new-comment-signal' data-asset-id='{assetId}'></div>");

        return dto;
    }

    public async Task<CommentDto?> UpdateCommentAsync(int commentId, string authorId, string newContent)
    {
        var comment = await _db.Comments
            .Include(c => c.Author)
            .FirstOrDefaultAsync(c => c.Id == commentId);

        if (comment == null) return null;
        if (comment.AuthorId != authorId)
            throw new UnauthorizedAccessException("You can only edit your own comments.");

        comment.Content = newContent;
        comment.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return new CommentDto
        {
            Id = comment.Id,
            AssetId = comment.AssetId,
            AuthorId = comment.AuthorId,
            AuthorDisplayName = comment.Author?.DisplayName ?? "Unknown",
            Content = comment.Content,
            ParentCommentId = comment.ParentCommentId,
            CreatedAt = comment.CreatedAt,
            UpdatedAt = comment.UpdatedAt,
            IsOwner = true
        };
    }

    public async Task<bool> DeleteCommentAsync(int commentId, string authorId)
    {
        var comment = await _db.Comments.FindAsync(commentId);
        if (comment == null) return false;
        if (comment.AuthorId != authorId)
            throw new UnauthorizedAccessException("You can only delete your own comments.");

        comment.Content = "[deleted]";
        comment.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<CommentDto>> GetCommentsAsync(int assetId, string? currentUserId = null)
    {
        var comments = await _db.Comments
            .Where(c => c.AssetId == assetId && c.ParentCommentId == null)
            .Include(c => c.Author)
            .Include(c => c.Replies.OrderBy(r => r.CreatedAt))
                .ThenInclude(r => r.Author)
            .OrderByDescending(c => c.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        return comments.Select(c => MapToDto(c, currentUserId)).ToList();
    }

    public async Task<int> GetCommentCountAsync(int assetId)
    {
        return await _db.Comments.CountAsync(c => c.AssetId == assetId);
    }

    private static CommentDto MapToDto(Comment comment, string? currentUserId)
    {
        return new CommentDto
        {
            Id = comment.Id,
            AssetId = comment.AssetId,
            AuthorId = comment.AuthorId,
            AuthorDisplayName = comment.Author?.DisplayName ?? "Unknown",
            Content = comment.Content,
            ParentCommentId = comment.ParentCommentId,
            CreatedAt = comment.CreatedAt,
            UpdatedAt = comment.UpdatedAt,
            IsOwner = currentUserId != null && comment.AuthorId == currentUserId,
            Replies = comment.Replies?
                .Select(r => MapToDto(r, currentUserId))
                .ToList() ?? new()
        };
    }
}
