using MediaFlows.Shared.DTOs;

namespace MediaFlows.Shared.Interfaces;

public interface IAnalyticsService
{
    Task<AnalyticsSnapshotDto> GetCurrentSnapshotAsync();
    Task<AnalyticsSnapshotDto> GetCloudWatchMetricsAsync();
    Task<List<DailyUploadCountDto>> GetDailyUploadCountsAsync(int days);
    Task<List<StorageByTypeDto>> GetStorageByTypeAsync();
}
