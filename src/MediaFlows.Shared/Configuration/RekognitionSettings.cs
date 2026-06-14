namespace MediaFlows.Shared.Configuration;

public class RekognitionSettings
{
    public bool Enabled { get; set; } = false;
    public string BucketName { get; set; } = null!;
    public string AssetUploadedTopicArn { get; set; } = null!;
}
