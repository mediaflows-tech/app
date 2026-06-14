namespace MediaFlows.Shared.DTOs;

public class TrendingAssetDto
{
    public int AssetId { get; set; }
    public string Title { get; set; } = null!;
    public string? ThumbnailUrl { get; set; }
    public int Score { get; set; }
    public int ViewCount { get; set; }
}
