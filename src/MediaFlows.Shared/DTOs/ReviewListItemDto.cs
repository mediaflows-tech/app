using MediaFlows.Shared.Models.Enums;

namespace MediaFlows.Shared.DTOs;

public class ReviewListItemDto
{
    public int AssetId { get; set; }
    public string Title { get; set; } = null!;
    public string? ThumbnailUrl { get; set; }
    public string ContentType { get; set; } = null!;
    public string CreatorId { get; set; } = null!;
    public string CreatorName { get; set; } = null!;
    public AssetStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastReviewedAt { get; set; }
    public int ReviewCount { get; set; }
    public long FileSize { get; set; }
}
