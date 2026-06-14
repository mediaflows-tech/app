using MediaFlows.Shared.DTOs;

namespace MediaFlows.Web.Controllers.Api.Models;

public class AdminDashboardViewModel
{
    public int TotalUsers { get; set; }
    public int TotalAssets { get; set; }
    public string StorageUsedFormatted { get; set; } = "0 MB";
    public int PendingReviews { get; set; }
    public string SystemHealth { get; set; } = "Healthy";
    public List<CloudWatchAlarmDto> ActiveAlarms { get; set; } = new();

    // Chart data
    public List<string> ActivityLabels { get; set; } = new();
    public List<int> ActivityData { get; set; } = new();
    public List<string> StorageTypeLabels { get; set; } = new();
    public List<long> StorageTypeData { get; set; } = new();

    public static AdminDashboardViewModel FromSnapshot(
        AnalyticsSnapshotDto snapshot,
        List<DailyUploadCountDto> dailyUploads,
        List<StorageByTypeDto> storageByType)
    {
        return new AdminDashboardViewModel
        {
            TotalUsers = snapshot.TotalUsers,
            TotalAssets = snapshot.TotalAssets,
            StorageUsedFormatted = FormatBytes(snapshot.StorageUsedBytes),
            PendingReviews = snapshot.PendingReviews,
            SystemHealth = snapshot.SystemHealth,
            ActiveAlarms = snapshot.ActiveAlarms,
            ActivityLabels = dailyUploads.Select(d => d.Date.ToString("ddd")).ToList(),
            ActivityData = dailyUploads.Select(d => d.Count).ToList(),
            StorageTypeLabels = storageByType.Select(s => s.Category).ToList(),
            StorageTypeData = storageByType.Select(s => s.Bytes).ToList()
        };
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int i = 0;
        double dblBytes = bytes;
        while (dblBytes >= 1024 && i < suffixes.Length - 1)
        {
            dblBytes /= 1024;
            i++;
        }
        return $"{dblBytes:0.##} {suffixes[i]}";
    }
}
