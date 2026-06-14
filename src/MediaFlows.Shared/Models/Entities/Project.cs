namespace MediaFlows.Shared.Models.Entities;

public class Project : IHasTimestamps
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string OwnerId { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public AppUser Owner { get; set; } = null!;
    public ICollection<MediaAsset> Assets { get; set; } = new List<MediaAsset>();
}
