namespace MediaFlows.Shared.DTOs;

public class AnalyticsSnapshotDto
{
    public int TotalUsers { get; set; }
    public int TotalAssets { get; set; }
    public long StorageUsedBytes { get; set; }
    public int PendingReviews { get; set; }
    public int UploadsPerMinute { get; set; }
    public int ReviewsPerMinute { get; set; }
    public double CpuUtilization { get; set; }
    public double MemoryUtilization { get; set; }
    public double RequestLatencyP95 { get; set; }
    public double ErrorRate { get; set; }
    public int ErrorCount { get; set; }
    public int LambdaColdStarts { get; set; }
    public double? EstimatedDailyCost { get; set; }
    public string SystemHealth { get; set; } = "Healthy";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public List<CloudWatchAlarmDto> ActiveAlarms { get; set; } = new();
}

public class CloudWatchAlarmDto
{
    public string AlarmName { get; set; } = null!;
    public string StateValue { get; set; } = null!;
    public string MetricName { get; set; } = null!;
    public DateTime StateUpdatedTimestamp { get; set; }
}

public class DailyUploadCountDto
{
    public DateTime Date { get; set; }
    public int Count { get; set; }
}

public class StorageByTypeDto
{
    public string Category { get; set; } = null!;
    public long Bytes { get; set; }
}
