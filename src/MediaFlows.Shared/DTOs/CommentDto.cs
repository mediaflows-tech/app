namespace MediaFlows.Shared.DTOs;

public class CommentDto
{
    public int Id { get; set; }
    public int AssetId { get; set; }
    public string AuthorId { get; set; } = null!;
    public string AuthorDisplayName { get; set; } = null!;
    public string Content { get; set; } = null!;
    public int? ParentCommentId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsOwner { get; set; }
    public List<CommentDto> Replies { get; set; } = new();
}
