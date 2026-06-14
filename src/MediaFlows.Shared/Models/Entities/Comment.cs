namespace MediaFlows.Shared.Models.Entities;

public class Comment : IHasTimestamps
{
    public int Id { get; set; }
    public int AssetId { get; set; }
    public string AuthorId { get; set; } = null!;
    public string Content { get; set; } = null!;
    public int? ParentCommentId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public MediaAsset Asset { get; set; } = null!;
    public AppUser Author { get; set; } = null!;
    public Comment? ParentComment { get; set; }
    public ICollection<Comment> Replies { get; set; } = new List<Comment>();
}
