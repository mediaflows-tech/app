namespace MediaFlows.Shared.DTOs;

public class UploadPresignedUrlResponse
{
    public string UploadUrl { get; set; } = null!;
    public string S3Key { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
}
