namespace MediaFlows.Shared.Configuration;

public class S3Settings
{
    public string BucketName { get; set; } = null!;
    public string Region { get; set; } = "ap-southeast-1";
    public string CloudFrontDomain { get; set; } = null!;
    public int PresignedUrlExpirationMinutes { get; set; } = 15;
    public long MaxFileSizeBytes { get; set; } = 5L * 1024 * 1024 * 1024;
}
