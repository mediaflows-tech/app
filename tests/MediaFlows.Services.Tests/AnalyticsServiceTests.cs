using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using Amazon.CloudWatchLogs;
using Amazon.CostExplorer;
using Amazon.CostExplorer.Model;
using FluentAssertions;
using MediaFlows.Data;
using MediaFlows.Shared.Models.Entities;
using MediaFlows.Shared.Models.Enums;
using MediaFlows.Tests.Common;
using MediaFlows.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using Moq;

namespace MediaFlows.Services.Tests;

public class AnalyticsServiceTests : IDisposable
{
    private readonly TestDbContext _db;
    private readonly Mock<IAmazonCloudWatch> _cloudWatch;
    private readonly Mock<IAmazonCloudWatchLogs> _cloudWatchLogs;
    private readonly Mock<IAmazonCostExplorer> _costExplorer;
    private readonly AnalyticsService _sut;

    public AnalyticsServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ReplaceService<IModelCacheKeyFactory, TestModelCacheKeyFactory>()
            .Options;
        _db = new TestDbContext(options);

        _cloudWatch = new Mock<IAmazonCloudWatch>();
        _cloudWatch.Setup(x => x.GetMetricDataAsync(It.IsAny<GetMetricDataRequest>(), default))
            .ReturnsAsync(new GetMetricDataResponse
            {
                MetricDataResults = new List<MetricDataResult>
                {
                    new() { Id = "cpu", Values = new List<double> { 45.5 } },
                    new() { Id = "latency", Values = new List<double> { 0.250 } },
                    new() { Id = "errors", Values = new List<double> { 2 } },
                    new() { Id = "requests", Values = new List<double> { 100 } }
                }
            });
        _cloudWatch.Setup(x => x.DescribeAlarmsAsync(It.IsAny<DescribeAlarmsRequest>(), default))
            .ReturnsAsync(new DescribeAlarmsResponse
            {
                MetricAlarms = new List<MetricAlarm>()
            });

        _cloudWatchLogs = new Mock<IAmazonCloudWatchLogs>();

        _costExplorer = new Mock<IAmazonCostExplorer>();
        _costExplorer.Setup(x => x.GetCostAndUsageAsync(It.IsAny<GetCostAndUsageRequest>(), default))
            .ReturnsAsync(new GetCostAndUsageResponse { ResultsByTime = new List<ResultByTime>() });

        var logger = new Mock<ILogger<AnalyticsService>>();
        _sut = new AnalyticsService(_db, _cloudWatch.Object, _cloudWatchLogs.Object, _costExplorer.Object, logger.Object);
    }

    [Fact]
    public async Task GetCurrentSnapshotAsync_ReturnsDatabaseMetrics()
    {
        _db.AppUsers.Add(new AppUser
        {
            CognitoSub = "sub-1",
            Email = "a@test.com",
            DisplayName = "Test",
            Role = "Admin",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        });
        _db.MediaAssets.AddRange(
            new MediaAsset
            {
                CreatorId = "sub-1",
                Title = "img1.jpg",
                S3Key = "k1",
                ContentType = "image/jpeg",
                FileSize = 1024 * 1024,
                Status = AssetStatus.Approved
            },
            new MediaAsset
            {
                CreatorId = "sub-1",
                Title = "img2.jpg",
                S3Key = "k2",
                ContentType = "image/jpeg",
                FileSize = 2048 * 1024,
                Status = AssetStatus.PendingReview
            }
        );
        await _db.SaveChangesAsync();

        var snapshot = await _sut.GetCurrentSnapshotAsync();

        snapshot.TotalUsers.Should().Be(1);
        snapshot.TotalAssets.Should().Be(2);
        snapshot.StorageUsedBytes.Should().Be(3 * 1024 * 1024);
        snapshot.PendingReviews.Should().Be(1);
        snapshot.SystemHealth.Should().Be("Healthy");
    }

    [Fact]
    public async Task GetCurrentSnapshotAsync_ReportsCloudWatchMetrics()
    {
        var snapshot = await _sut.GetCurrentSnapshotAsync();

        snapshot.CpuUtilization.Should().Be(45.5);
        snapshot.RequestLatencyP95.Should().Be(250);
        snapshot.ErrorRate.Should().Be(2);
    }

    [Fact]
    public async Task GetCloudWatchMetricsAsync_FiltersAlbDiscoveryToRecentlyActiveMetrics()
    {
        ListMetricsRequest? listMetricsRequest = null;

        _cloudWatch.Setup(x => x.ListMetricsAsync(It.IsAny<ListMetricsRequest>(), default))
            .Callback<ListMetricsRequest, CancellationToken>((request, _) => listMetricsRequest = request)
            .ReturnsAsync(new ListMetricsResponse
            {
                Metrics = new List<Amazon.CloudWatch.Model.Metric>
                {
                    new()
                    {
                        Dimensions = new List<Amazon.CloudWatch.Model.Dimension>
                        {
                            new() { Name = "LoadBalancer", Value = "app/live-alb/1234567890abcdef" }
                        }
                    }
                }
            });

        var snapshot = await _sut.GetCloudWatchMetricsAsync();

        listMetricsRequest.Should().NotBeNull();
        listMetricsRequest!.Namespace.Should().Be("AWS/ApplicationELB");
        listMetricsRequest.MetricName.Should().Be("TargetResponseTime");
        listMetricsRequest.RecentlyActive.Should().Be("PT3H");
        snapshot.RequestLatencyP95.Should().Be(250);
    }

    [Fact]
    public async Task GetCurrentSnapshotAsync_HandlesCloudWatchFailure()
    {
        _cloudWatch.Setup(x => x.GetMetricDataAsync(It.IsAny<GetMetricDataRequest>(), default))
            .ThrowsAsync(new AmazonCloudWatchException("Service unavailable"));

        var snapshot = await _sut.GetCurrentSnapshotAsync();

        snapshot.CpuUtilization.Should().Be(0);
        snapshot.SystemHealth.Should().Be("Healthy");
    }

    public void Dispose() => _db.Dispose();
}
