using MediaFlows.Shared.Models.Enums;

namespace MediaFlows.Shared.DTOs;

public class MediaAssetSummaryDto
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string? ThumbnailUrl { get; set; }
    public string? PreviewUrl { get; set; }
    public AssetStatus Status { get; set; }
    public string ContentType { get; set; } = null!;
    public long FileSize { get; set; }
    public string CreatorName { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public List<string> Tags { get; set; } = new();
}
