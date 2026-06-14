namespace MediaFlows.Shared.Models.ValueObjects;

public class MediaMetadata
{
    public int? Width { get; set; }
    public int? Height { get; set; }
    public double? DurationSeconds { get; set; }
    public string? Codec { get; set; }
    public string? Format { get; set; }
    public long? Bitrate { get; set; }
    public List<AssetTag> AutoTags { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public List<ExifTag> ExifTags { get; set; } = new();
    public ModerationResult? Moderation { get; set; }
}

public class AssetTag
{
    public string Name { get; set; } = null!;
    public float Confidence { get; set; }
}

public class ExifTag
{
    public string Key { get; set; } = null!;
    public string Value { get; set; } = null!;
}

public class ModerationResult
{
    public bool IsSafe { get; set; }
    public List<ModerationLabel> Labels { get; set; } = new();
    public DateTime ScannedAt { get; set; }
}

public class ModerationLabel
{
    public string Name { get; set; } = null!;
    public string ParentName { get; set; } = null!;
    public float Confidence { get; set; }
}
