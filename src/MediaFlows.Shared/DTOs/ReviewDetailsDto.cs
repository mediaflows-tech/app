using MediaFlows.Shared.Models.Enums;

namespace MediaFlows.Shared.DTOs;

public class ReviewDetailsDto
{
    public int AssetId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string ContentType { get; set; } = null!;
    public string S3Key { get; set; } = null!;
    public string CreatorId { get; set; } = null!;
    public string CreatorName { get; set; } = null!;
    public AssetStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public long FileSize { get; set; }
    public DateTime? ScheduledPublishAt { get; set; }
    public List<ReviewHistoryItemDto> ReviewHistory { get; set; } = new();
}

public class ReviewHistoryItemDto
{
    public string ReviewerName { get; set; } = null!;
    public ReviewDecision Decision { get; set; }
    public string? Comments { get; set; }
    public DateTime ReviewedAt { get; set; }
}
