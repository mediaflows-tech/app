namespace MediaFlows.Shared.Models.Entities;

public class Bookmark : IHasTimestamps
{
    public int Id { get; set; }
    public string UserId { get; set; } = null!;
    public int AssetId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public AppUser User { get; set; } = null!;
    public MediaAsset Asset { get; set; } = null!;
}
