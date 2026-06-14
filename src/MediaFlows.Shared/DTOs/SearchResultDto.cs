namespace MediaFlows.Shared.DTOs;

public class SearchResultDto
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? PreviewUrl { get; set; }
    public string ContentType { get; set; } = null!;
    public string CreatorName { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public string? Headline { get; set; }
}
