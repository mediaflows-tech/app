using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using CE = Amazon.CostExplorer;
using MediaFlows.Data;
using MediaFlows.Shared.DTOs;
using MediaFlows.Shared.Interfaces;
using MediaFlows.Shared.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace MediaFlows.Web.Services;

public class AnalyticsService : IAnalyticsService
{
    private readonly ApplicationDbContext _db;
    private readonly IAmazonCloudWatch _cloudWatch;
    private readonly IAmazonCloudWatchLogs _cloudWatchLogs;
    private readonly CE.IAmazonCostExplorer _costExplorer;
    private readonly ILogger<AnalyticsService> _logger;
    private static string? _instanceId;
    private string? _albDimension;

    // In-memory caches — Logs Insights and Cost Explorer are slow and charged per request
    private static int? _cachedColdStarts;
    private static DateTime _coldStartsLastFetch = DateTime.MinValue;
    private static readonly TimeSpan ColdStartsCacheTtl = TimeSpan.FromMinutes(1);

    private static double? _cachedCost;
    private static DateTime _costLastFetch = DateTime.MinValue;
    private static readonly TimeSpan CostCacheTtl = TimeSpan.FromHours(1);

    public AnalyticsService(
        ApplicationDbContext db,
        IAmazonCloudWatch cloudWatch,
        IAmazonCloudWatchLogs cloudWatchLogs,
        CE.IAmazonCostExplorer costExplorer,
        ILogger<AnalyticsService> logger)
    {
        _db = db;
        _cloudWatch = cloudWatch;
        _cloudWatchLogs = cloudWatchLogs;
        _costExplorer = costExplorer;
        _logger = logger;
    }

    private static async Task<string?> GetInstanceIdAsync()
    {
        if (_instanceId != null) return _instanceId;
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var token = await client.SendAsync(new HttpRequestMessage(HttpMethod.Put,
                "http://169.254.169.254/latest/api/token")
            {
                Headers = { { "X-aws-ec2-metadata-token-ttl-seconds", "60" } }
            });
            var tokenValue = await token.Content.ReadAsStringAsync();
            var request = new HttpRequestMessage(HttpMethod.Get,
                "http://169.254.169.254/latest/meta-data/instance-id");
            request.Headers.Add("X-aws-ec2-metadata-token", tokenValue);
            _instanceId = await (await client.SendAsync(request)).Content.ReadAsStringAsync();
            return _instanceId;
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> GetAlbDimensionAsync()
    {
        if (_albDimension != null) return _albDimension;
        try
        {
            var metrics = await _cloudWatch.ListMetricsAsync(new ListMetricsRequest
            {
                Namespace = "AWS/ApplicationELB",
                MetricName = "TargetResponseTime",
                RecentlyActive = "PT3H"
            });
            var lbDim = metrics.Metrics
                .SelectMany(m => m.Dimensions)
                .FirstOrDefault(d => d.Name == "LoadBalancer");
            _albDimension = lbDim?.Value;
            return _albDimension;
        }
        catch
        {
            return null;
        }
    }

    public async Task<AnalyticsSnapshotDto> GetCurrentSnapshotAsync()
    {
        var now = DateTime.UtcNow;
        var fiveMinutesAgo = now.AddMinutes(-5);

        var totalUsers = await _db.AppUsers.CountAsync();
        var totalAssets = await _db.MediaAssets.CountAsync();
        var pendingReviews = await _db.MediaAssets
            .CountAsync(a => a.Status == AssetStatus.PendingReview);

        var recentUploads = await _db.MediaAssets
            .CountAsync(a => a.CreatedAt >= fiveMinutesAgo);
        var recentReviews = await _db.Reviews
            .CountAsync(r => r.ReviewedAt >= fiveMinutesAgo);

        var storageUsed = await _db.MediaAssets
            .SumAsync(a => (long?)a.FileSize) ?? 0;

        var cloudWatchMetrics = await GetCloudWatchMetricsAsync();

        return new AnalyticsSnapshotDto
        {
            TotalUsers = totalUsers,
            TotalAssets = totalAssets,
            StorageUsedBytes = storageUsed,
            PendingReviews = pendingReviews,
            UploadsPerMinute = (int)Math.Ceiling(recentUploads / 5.0),
            ReviewsPerMinute = (int)Math.Ceiling(recentReviews / 5.0),
            CpuUtilization = cloudWatchMetrics.CpuUtilization,
            MemoryUtilization = cloudWatchMetrics.MemoryUtilization,
            RequestLatencyP95 = cloudWatchMetrics.RequestLatencyP95,
            ErrorRate = cloudWatchMetrics.ErrorRate,
            ErrorCount = cloudWatchMetrics.ErrorCount,
            LambdaColdStarts = cloudWatchMetrics.LambdaColdStarts,
            EstimatedDailyCost = cloudWatchMetrics.EstimatedDailyCost,
            SystemHealth = cloudWatchMetrics.ActiveAlarms.Any(a => a.StateValue == "ALARM")
                ? "Degraded" : "Healthy",
            Timestamp = now,
            ActiveAlarms = cloudWatchMetrics.ActiveAlarms
        };
    }

    public async Task<AnalyticsSnapshotDto> GetCloudWatchMetricsAsync()
    {
        var now = DateTime.UtcNow;
        // 1h window so the errors/requests sums cover the last hour for the "Error Rate (Last 1h)" card.
        var start = now.AddHours(-1);

        try
        {
            var instanceId = await GetInstanceIdAsync();
            var albName = await GetAlbDimensionAsync();
            _logger.LogInformation("CloudWatch dimensions — InstanceId: {InstanceId}, ALB: {AlbName}",
                instanceId ?? "(null)", albName ?? "(null)");
            var queries = new List<MetricDataQuery>();

            if (instanceId != null)
            {
                queries.Add(new MetricDataQuery
                {
                    Id = "cpu",
                    MetricStat = new MetricStat
                    {
                        Metric = new Metric
                        {
                            Namespace = "AWS/EC2",
                            MetricName = "CPUUtilization",
                            Dimensions = new List<Dimension>
                            {
                                new() { Name = "InstanceId", Value = instanceId }
                            }
                        },
                        Period = 300,
                        Stat = "Average"
                    }
                });
                queries.Add(new MetricDataQuery
                {
                    Id = "memory",
                    MetricStat = new MetricStat
                    {
                        Metric = new Metric
                        {
                            Namespace = "CWAgent",
                            MetricName = "mem_used_percent",
                            Dimensions = new List<Dimension>
                            {
                                new() { Name = "InstanceId", Value = instanceId }
                            }
                        },
                        Period = 300,
                        Stat = "Average"
                    }
                });
            }

            if (albName != null)
            {
                var albDimensions = new List<Dimension>
                {
                    new() { Name = "LoadBalancer", Value = albName }
                };

                queries.Add(new MetricDataQuery
                {
                    Id = "latency",
                    MetricStat = new MetricStat
                    {
                        Metric = new Metric
                        {
                            Namespace = "AWS/ApplicationELB",
                            MetricName = "TargetResponseTime",
                            Dimensions = albDimensions
                        },
                        Period = 300,
                        Stat = "p95"
                    }
                });
                queries.Add(new MetricDataQuery
                {
                    Id = "errors",
                    MetricStat = new MetricStat
                    {
                        Metric = new Metric
                        {
                            Namespace = "AWS/ApplicationELB",
                            MetricName = "HTTPCode_Target_5XX_Count",
                            Dimensions = albDimensions
                        },
                        Period = 3600,
                        Stat = "Sum"
                    }
                });
                queries.Add(new MetricDataQuery
                {
                    Id = "requests",
                    MetricStat = new MetricStat
                    {
                        Metric = new Metric
                        {
                            Namespace = "AWS/ApplicationELB",
                            MetricName = "RequestCount",
                            Dimensions = albDimensions
                        },
                        Period = 3600,
                        Stat = "Sum"
                    }
                });
            }

            var metricResponse = await _cloudWatch.GetMetricDataAsync(new GetMetricDataRequest
            {
                StartTime = start,
                EndTime = now,
                MetricDataQueries = queries
            });

            var alarmsResponse = await _cloudWatch.DescribeAlarmsAsync(new DescribeAlarmsRequest
            {
                StateValue = StateValue.ALARM,
                MaxRecords = 10
            });

            var activeAlarms = alarmsResponse.MetricAlarms.Select(a => new CloudWatchAlarmDto
            {
                AlarmName = a.AlarmName,
                StateValue = a.StateValue.Value,
                MetricName = a.MetricName,
                StateUpdatedTimestamp = a.StateUpdatedTimestamp ?? DateTime.UtcNow
            }).ToList();

            var totalRequests = GetLatestValue(metricResponse, "requests");
            var errorCount = GetLatestValue(metricResponse, "errors");

            // Fetch the slow/expensive metrics with caching
            var coldStarts = await GetColdStartsCachedAsync();
            var estCost = await GetEstimatedCostLast24hCachedAsync();

            return new AnalyticsSnapshotDto
            {
                CpuUtilization = GetLatestValue(metricResponse, "cpu"),
                MemoryUtilization = GetLatestValue(metricResponse, "memory"),
                RequestLatencyP95 = GetLatestValue(metricResponse, "latency") * 1000,
                ErrorRate = totalRequests > 0 ? Math.Round(errorCount / totalRequests * 100, 2) : 0,
                ErrorCount = (int)errorCount,
                LambdaColdStarts = coldStarts,
                EstimatedDailyCost = estCost,
                ActiveAlarms = activeAlarms,
                Timestamp = now
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch CloudWatch metrics");
            return new AnalyticsSnapshotDto { Timestamp = now };
        }
    }

    public async Task<List<DailyUploadCountDto>> GetDailyUploadCountsAsync(int days)
    {
        var today = DateTime.UtcNow.Date;
        var startDate = today.AddDays(-(days - 1));

        var uploads = await _db.MediaAssets
            .Where(a => a.CreatedAt >= startDate)
            .GroupBy(a => a.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Date, x => x.Count);

        var result = new List<DailyUploadCountDto>();
        for (var date = startDate; date <= today; date = date.AddDays(1))
        {
            result.Add(new DailyUploadCountDto
            {
                Date = date,
                Count = uploads.GetValueOrDefault(date, 0)
            });
        }

        return result;
    }

    public async Task<List<StorageByTypeDto>> GetStorageByTypeAsync()
    {
        var raw = await _db.MediaAssets
            .GroupBy(a => a.ContentType)
            .Select(g => new { ContentType = g.Key, Bytes = g.Sum(a => (long?)a.FileSize) ?? 0 })
            .ToListAsync();

        var categories = new Dictionary<string, long>
        {
            ["Images"] = 0,
            ["Video"] = 0,
            ["Audio"] = 0,
            ["Other"] = 0
        };

        foreach (var item in raw)
        {
            var category = CategorizeContentType(item.ContentType);
            categories[category] += item.Bytes;
        }

        return categories
            .Select(kvp => new StorageByTypeDto { Category = kvp.Key, Bytes = kvp.Value })
            .ToList();
    }

    private static string CategorizeContentType(string contentType)
    {
        if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return "Images";
        if (contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
            return "Video";
        if (contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
            return "Audio";
        return "Other";
    }

    private static double GetLatestValue(GetMetricDataResponse response, string id)
    {
        var result = response.MetricDataResults.FirstOrDefault(r => r.Id == id);
        return result?.Values.FirstOrDefault() ?? 0;
    }

    private async Task<int> GetColdStartsCachedAsync()
    {
        if (_cachedColdStarts.HasValue && DateTime.UtcNow - _coldStartsLastFetch < ColdStartsCacheTtl)
            return _cachedColdStarts.Value;

        var value = await GetColdStartsAsync();
        _cachedColdStarts = value;
        _coldStartsLastFetch = DateTime.UtcNow;
        return value;
    }

    private async Task<int> GetColdStartsAsync()
    {
        try
        {
            var logGroupsResp = await _cloudWatchLogs.DescribeLogGroupsAsync(new DescribeLogGroupsRequest
            {
                LogGroupNamePrefix = "/aws/lambda/mediaflows-"
            });
            if (logGroupsResp.LogGroups.Count == 0) return 0;

            var now = DateTime.UtcNow;
            var oneHourAgo = now.AddHours(-1);

            var startResp = await _cloudWatchLogs.StartQueryAsync(new StartQueryRequest
            {
                LogGroupNames = logGroupsResp.LogGroups.Select(g => g.LogGroupName).ToList(),
                StartTime = ((DateTimeOffset)oneHourAgo).ToUnixTimeSeconds(),
                EndTime = ((DateTimeOffset)now).ToUnixTimeSeconds(),
                QueryString = "fields @message | filter @message like /Init Duration:/ | stats count() as coldStarts"
            });

            // Poll for completion (max ~5s)
            for (var i = 0; i < 10; i++)
            {
                await Task.Delay(500);
                var result = await _cloudWatchLogs.GetQueryResultsAsync(new GetQueryResultsRequest
                {
                    QueryId = startResp.QueryId
                });

                if (result.Status == QueryStatus.Complete)
                {
                    if (result.Results.Count == 0) return 0;
                    var field = result.Results[0].FirstOrDefault(f => f.Field == "coldStarts");
                    return field != null && int.TryParse(field.Value, out var count) ? count : 0;
                }
                if (result.Status == QueryStatus.Failed || result.Status == QueryStatus.Cancelled)
                    return 0;
            }

            _logger.LogWarning("Cold starts query timed out");
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query Lambda cold starts");
            return 0;
        }
    }

    private async Task<double?> GetEstimatedCostLast24hCachedAsync()
    {
        if (_costLastFetch != DateTime.MinValue && DateTime.UtcNow - _costLastFetch < CostCacheTtl)
            return _cachedCost;

        var value = await GetEstimatedCostLast24hAsync();
        _cachedCost = value;
        _costLastFetch = DateTime.UtcNow;
        return value;
    }

    // RECORD_TYPE=Usage excludes Free Tier credits so the value matches the
    // pre-discount Service Cost Breakdown shown in the AWS Billing console.
    private async Task<double?> GetEstimatedCostLast24hAsync()
    {
        try
        {
            var now = DateTime.UtcNow;
            var response = await _costExplorer.GetCostAndUsageAsync(new CE.Model.GetCostAndUsageRequest
            {
                TimePeriod = new CE.Model.DateInterval
                {
                    // 7-day window absorbs CE's 8–24h publication lag.
                    Start = now.AddDays(-7).ToString("yyyy-MM-dd"),
                    End = now.AddDays(1).ToString("yyyy-MM-dd")
                },
                Granularity = CE.Granularity.DAILY,
                Metrics = new List<string> { "UnblendedCost" },
                Filter = new CE.Model.Expression
                {
                    Dimensions = new CE.Model.DimensionValues
                    {
                        Key = CE.Dimension.RECORD_TYPE,
                        Values = new List<string> { "Usage" }
                    }
                }
            });

            if (response.ResultsByTime == null || response.ResultsByTime.Count == 0)
            {
                _logger.LogInformation("Cost Explorer returned no daily buckets");
                return null;
            }

            for (var i = response.ResultsByTime.Count - 1; i >= 0; i--)
            {
                var bucket = response.ResultsByTime[i];
                if (bucket.Total == null || !bucket.Total.TryGetValue("UnblendedCost", out var metric))
                    continue;
                if (!double.TryParse(metric.Amount, out var cost) || cost <= 0)
                    continue;

                _logger.LogInformation(
                    "Cost Explorer {Date}: ${Cost:F2} (RECORD_TYPE=Usage)",
                    bucket.TimePeriod.Start, cost);
                return Math.Round(cost, 2);
            }

            _logger.LogInformation("Cost Explorer returned no non-zero day in the last 7 days");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query Cost Explorer");
            return null;
        }
    }
}
