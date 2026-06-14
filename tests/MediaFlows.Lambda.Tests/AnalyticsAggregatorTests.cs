using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.Core;
using Amazon.Lambda.TestUtilities;
using FluentAssertions;
using MediaFlows.Lambda.AnalyticsAggregator;
using MediaFlows.Shared.Models.DynamoDb;
using Moq;

namespace MediaFlows.Lambda.Tests;

public class AnalyticsAggregatorTests
{
    private readonly Mock<IDynamoDBContext> _dynamoContext;
    private readonly Function _sut;
    private readonly TestLambdaContext _lambdaContext;

    public AnalyticsAggregatorTests()
    {
        _dynamoContext = new Mock<IDynamoDBContext>();
        _sut = new Function(_dynamoContext.Object);
        _lambdaContext = new TestLambdaContext();
    }

    [Fact]
    public async Task FunctionHandler_AggregatesTopViewCounters()
    {
        var counters = new List<ViewCounter>
        {
            new() { AssetId = "1", ViewCount = 100, LastViewedAt = DateTime.UtcNow },
            new() { AssetId = "2", ViewCount = 500, LastViewedAt = DateTime.UtcNow },
            new() { AssetId = "3", ViewCount = 250, LastViewedAt = DateTime.UtcNow },
        };
        var mockSearch = new Mock<IAsyncSearch<ViewCounter>>();
        mockSearch.Setup(x => x.GetRemainingAsync(default)).ReturnsAsync(counters);
        _dynamoContext.Setup(x => x.ScanAsync<ViewCounter>(
            It.IsAny<IEnumerable<ScanCondition>>()))
            .Returns(mockSearch.Object);
        var writtenItems = new List<TrendingDataItem>();
        var mockBatchWrite = new Mock<IBatchWrite<TrendingDataItem>>();
        mockBatchWrite.Setup(x => x.AddPutItem(It.IsAny<TrendingDataItem>()))
            .Callback<TrendingDataItem>(item => writtenItems.Add(item));
        _dynamoContext.Setup(x => x.CreateBatchWrite<TrendingDataItem>())
            .Returns(mockBatchWrite.Object);
        _dynamoContext.Setup(x => x.ExecuteBatchWriteAsync(
            It.IsAny<IBatchWrite[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var mockCleanupSearch = new Mock<IAsyncSearch<TrendingDataItem>>();
        mockCleanupSearch.Setup(x => x.GetRemainingAsync(default))
            .ReturnsAsync(new List<TrendingDataItem>());
        _dynamoContext.Setup(x => x.QueryAsync<TrendingDataItem>(
            It.IsAny<object>()))
            .Returns(mockCleanupSearch.Object);

        await _sut.FunctionHandler(new { }, _lambdaContext);

        writtenItems.Should().HaveCount(3);
        writtenItems[0].Score.Should().Be(500);
        writtenItems[1].Score.Should().Be(250);
        writtenItems[2].Score.Should().Be(100);
    }

    [Fact]
    public async Task FunctionHandler_HandlesEmptyCounters()
    {
        var mockSearch = new Mock<IAsyncSearch<ViewCounter>>();
        mockSearch.Setup(x => x.GetRemainingAsync(default)).ReturnsAsync(new List<ViewCounter>());
        _dynamoContext.Setup(x => x.ScanAsync<ViewCounter>(
            It.IsAny<IEnumerable<ScanCondition>>()))
            .Returns(mockSearch.Object);

        await _sut.FunctionHandler(new { }, _lambdaContext);

        _dynamoContext.Verify(x => x.CreateBatchWrite<TrendingDataItem>(), Times.Never);
    }

    [Fact]
    public async Task FunctionHandler_AggregatesEvenWhenInputResemblesApiRequest()
    {
        var counters = new List<ViewCounter>
        {
            new() { AssetId = "1", ViewCount = 100, LastViewedAt = DateTime.UtcNow }
        };
        var mockSearch = new Mock<IAsyncSearch<ViewCounter>>();
        mockSearch.Setup(x => x.GetRemainingAsync(default)).ReturnsAsync(counters);
        _dynamoContext.Setup(x => x.ScanAsync<ViewCounter>(
            It.IsAny<IEnumerable<ScanCondition>>()))
            .Returns(mockSearch.Object);
        var writtenItems = new List<TrendingDataItem>();
        var mockBatchWrite = new Mock<IBatchWrite<TrendingDataItem>>();
        mockBatchWrite.Setup(x => x.AddPutItem(It.IsAny<TrendingDataItem>()))
            .Callback<TrendingDataItem>(item => writtenItems.Add(item));
        _dynamoContext.Setup(x => x.CreateBatchWrite<TrendingDataItem>())
            .Returns(mockBatchWrite.Object);
        _dynamoContext.Setup(x => x.ExecuteBatchWriteAsync(
            It.IsAny<IBatchWrite[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var mockCleanupSearch = new Mock<IAsyncSearch<TrendingDataItem>>();
        mockCleanupSearch.Setup(x => x.GetRemainingAsync(default))
            .ReturnsAsync(new List<TrendingDataItem>());
        _dynamoContext.Setup(x => x.QueryAsync<TrendingDataItem>(
            It.IsAny<object>()))
            .Returns(mockCleanupSearch.Object);
        var apiShapedInput = new { httpMethod = "GET", path = "/api/analytics/trending" };

        await _sut.FunctionHandler(apiShapedInput, _lambdaContext);

        writtenItems.Should().HaveCount(1);
        writtenItems[0].Score.Should().Be(100);
    }

    [Fact]
    public async Task FunctionHandler_LimitsToTop50()
    {
        var counters = Enumerable.Range(1, 100)
            .Select(i => new ViewCounter
            {
                AssetId = i.ToString(),
                ViewCount = i,
                LastViewedAt = DateTime.UtcNow
            })
            .ToList();
        var mockSearch = new Mock<IAsyncSearch<ViewCounter>>();
        mockSearch.Setup(x => x.GetRemainingAsync(default)).ReturnsAsync(counters);
        _dynamoContext.Setup(x => x.ScanAsync<ViewCounter>(
            It.IsAny<IEnumerable<ScanCondition>>()))
            .Returns(mockSearch.Object);
        var writtenItems = new List<TrendingDataItem>();
        var mockBatchWrite = new Mock<IBatchWrite<TrendingDataItem>>();
        mockBatchWrite.Setup(x => x.AddPutItem(It.IsAny<TrendingDataItem>()))
            .Callback<TrendingDataItem>(item => writtenItems.Add(item));
        _dynamoContext.Setup(x => x.CreateBatchWrite<TrendingDataItem>())
            .Returns(mockBatchWrite.Object);
        _dynamoContext.Setup(x => x.ExecuteBatchWriteAsync(
            It.IsAny<IBatchWrite[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var mockCleanupSearch = new Mock<IAsyncSearch<TrendingDataItem>>();
        mockCleanupSearch.Setup(x => x.GetRemainingAsync(default))
            .ReturnsAsync(new List<TrendingDataItem>());
        _dynamoContext.Setup(x => x.QueryAsync<TrendingDataItem>(
            It.IsAny<object>()))
            .Returns(mockCleanupSearch.Object);

        await _sut.FunctionHandler(new { }, _lambdaContext);

        writtenItems.Should().HaveCount(50);
        writtenItems.First().Score.Should().Be(100);
    }
}
