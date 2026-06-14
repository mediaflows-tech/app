using System.ComponentModel.DataAnnotations;

namespace MediaFlows.Shared.DTOs;

public class CreateCommentRequest
{
    [Required]
    public int AssetId { get; set; }

    [Required]
    [MinLength(1)]
    [MaxLength(5000)]
    public string Content { get; set; } = null!;

    public int? ParentCommentId { get; set; }
}

public class UpdateCommentRequest
{
    [Required]
    public int CommentId { get; set; }

    [Required]
    [MinLength(1)]
    [MaxLength(5000)]
    public string Content { get; set; } = null!;
}
