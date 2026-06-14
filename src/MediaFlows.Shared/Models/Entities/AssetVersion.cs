namespace MediaFlows.Shared.Models.Entities;

public class AssetVersion : IHasTimestamps
{
    public int Id { get; set; }
    public int AssetId { get; set; }
    public int VersionNumber { get; set; }
    public string S3Key { get; set; } = null!;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = null!;
    public string? ChangeNotes { get; set; }
    public string UploadedById { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public MediaAsset Asset { get; set; } = null!;
    public AppUser UploadedBy { get; set; } = null!;
}
