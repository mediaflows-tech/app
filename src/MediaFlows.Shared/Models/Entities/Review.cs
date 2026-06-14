using MediaFlows.Shared.Models.Enums;

namespace MediaFlows.Shared.Models.Entities;

public class Review : IHasTimestamps
{
    public int Id { get; set; }
    public int AssetId { get; set; }
    public string ReviewerId { get; set; } = null!;
    public ReviewDecision Decision { get; set; }
    public string? Comments { get; set; }
    public DateTime ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public MediaAsset Asset { get; set; } = null!;
    public AppUser Reviewer { get; set; } = null!;
}
