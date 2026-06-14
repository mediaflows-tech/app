using MediaFlows.Shared.Models.Enums;
using MediaFlows.Shared.Models.ValueObjects;
using NpgsqlTypes;

namespace MediaFlows.Shared.Models.Entities;

public class MediaAsset : IHasTimestamps
{
    public int Id { get; set; }
    public int? ProjectId { get; set; }
    public string CreatorId { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string S3Key { get; set; } = null!;
    public string? ThumbnailUrl { get; set; }
    public string ContentType { get; set; } = null!;
    public long FileSize { get; set; }
    public AssetStatus Status { get; set; } = AssetStatus.Draft;
    public int? CurrentVersionId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? ScheduledPublishAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public MediaMetadata Metadata { get; set; } = new();
    public NpgsqlTsVector SearchVector { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public AppUser Creator { get; set; } = null!;
    public Project? Project { get; set; }
    public AssetVersion? CurrentVersion { get; set; }
    public ICollection<AssetVersion> Versions { get; set; } = new List<AssetVersion>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<Bookmark> Bookmarks { get; set; } = new List<Bookmark>();
}
