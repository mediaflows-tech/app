using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using MediaFlows.Shared.Configuration;
using MediaFlows.Shared.Models.DynamoDb;
using MediaFlows.Web.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace MediaFlows.Services.Tests;

public class DynamoDbServiceTests
{
    private readonly Mock<IAmazonDynamoDB> _clientMock = new();
    private readonly Mock<IDynamoDBContext> _contextMock = new();
    private readonly ILogger<DynamoDbService> _logger = NullLogger<DynamoDbService>.Instance;
    private readonly IOptions<DynamoDbSettings> _settings = Options.Create(new DynamoDbSettings());

    [Fact]
    public async Task IncrementViewCountAsync_ReturnsUpdatedCount()
    {
        _clientMock.Setup(c => c.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .ReturnsAsync(new UpdateItemResponse
            {
                Attributes = new Dictionary<string, AttributeValue>
                {
                    { "ViewCount", new AttributeValue { N = "5" } }
                }
            });

        var service = new DynamoDbService(_clientMock.Object, _contextMock.Object, _logger, _settings);
        var count = await service.IncrementViewCountAsync("asset-42");

        count.Should().Be(5);

        _clientMock.Verify(c => c.UpdateItemAsync(It.Is<UpdateItemRequest>(r =>
            r.TableName == "ViewCounters" &&
            r.Key["AssetId"].S == "asset-42" &&
            r.UpdateExpression.Contains("if_not_exists") &&
            r.ReturnValues == ReturnValue.UPDATED_NEW
        ), default), Times.Once);
    }

    [Fact]
    public async Task GetViewCountAsync_ReturnsZero_WhenItemNotExists()
    {
        _clientMock.Setup(c => c.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse
            {
                Item = null!
            });

        var service = new DynamoDbService(_clientMock.Object, _contextMock.Object, _logger, _settings);
        var count = await service.GetViewCountAsync("non-existent");

        count.Should().Be(0);
    }

    [Fact]
    public async Task GetViewCountAsync_ReturnsCount_WhenItemExists()
    {
        _clientMock.Setup(c => c.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse
            {
                Item = new Dictionary<string, AttributeValue>
                {
                    { "ViewCount", new AttributeValue { N = "42" } }
                }
            });

        var service = new DynamoDbService(_clientMock.Object, _contextMock.Object, _logger, _settings);
        var count = await service.GetViewCountAsync("asset-1");

        count.Should().Be(42);
    }

    [Fact]
    public async Task RecordActivityAsync_SavesItemToContext()
    {
        var service = new DynamoDbService(_clientMock.Object, _contextMock.Object, _logger, _settings);
        await service.RecordActivityAsync("user-1", "viewed", "MediaAsset", "42", "Viewed asset");

        _contextMock.Verify(c => c.SaveAsync(It.Is<ActivityFeedItem>(i =>
            i.UserId == "user-1" &&
            i.Action == "viewed" &&
            i.EntityType == "MediaAsset" &&
            i.EntityId == "42" &&
            i.ExpiresAt > 0
        ), default), Times.Once);
    }

    [Fact]
    public async Task AddActivityFeedItemAsync_SavesItem()
    {
        var item = new ActivityFeedItem
        {
            UserId = "user-1",
            Timestamp = DateTime.UtcNow.ToString("o"),
            Action = "uploaded",
            EntityType = "MediaAsset",
            EntityId = "10",
            Details = "Uploaded new asset",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds()
        };

        var service = new DynamoDbService(_clientMock.Object, _contextMock.Object, _logger, _settings);
        await service.AddActivityFeedItemAsync(item);

        _contextMock.Verify(c => c.SaveAsync(It.Is<ActivityFeedItem>(i =>
            i.UserId == "user-1" &&
            i.Action == "uploaded" &&
            i.EntityId == "10"
        ), default), Times.Once);
    }

    [Fact]
    public async Task GetTrendingAsync_QueriesByDateAndReturnsLimited()
    {
        var mockSearch = new Mock<AsyncSearch<TrendingDataItem>>();
        var trendingItems = new List<TrendingDataItem>
        {
            new() { TimeBucket = "2026-03-19", ScoreAssetId = "100#1", AssetId = 1, Score = 100 },
            new() { TimeBucket = "2026-03-19", ScoreAssetId = "050#2", AssetId = 2, Score = 50 },
            new() { TimeBucket = "2026-03-19", ScoreAssetId = "025#3", AssetId = 3, Score = 25 }
        };

        _contextMock.Setup(c => c.QueryAsync<TrendingDataItem>("2026-03-19"))
            .Returns(mockSearch.Object);
        mockSearch.Setup(s => s.GetRemainingAsync(default)).ReturnsAsync(trendingItems);

        var service = new DynamoDbService(_clientMock.Object, _contextMock.Object, _logger, _settings);
        var result = await service.GetTrendingAsync("2026-03-19", limit: 2);

        result.Should().HaveCount(2);
        result[0].Score.Should().Be(100);
        result[1].Score.Should().Be(50);
    }

    [Fact]
    public async Task GetActivityFeedAsync_QueriesByUserAndReturnsLimited()
    {
        var mockSearch = new Mock<AsyncSearch<ActivityFeedItem>>();
        var feedItems = new List<ActivityFeedItem>
        {
            new() { UserId = "u1", Action = "viewed", Timestamp = DateTime.UtcNow.ToString("o") },
            new() { UserId = "u1", Action = "uploaded", Timestamp = DateTime.UtcNow.AddMinutes(-1).ToString("o") }
        };

        _contextMock.Setup(c => c.QueryAsync<ActivityFeedItem>(
            "u1",
            QueryOperator.BeginsWith,
            It.IsAny<IEnumerable<object>>(),
            It.IsAny<DynamoDBOperationConfig>()))
            .Returns(mockSearch.Object);
        mockSearch.Setup(s => s.GetRemainingAsync(default)).ReturnsAsync(feedItems);

        var service = new DynamoDbService(_clientMock.Object, _contextMock.Object, _logger, _settings);
        var result = await service.GetActivityFeedAsync("u1", limit: 50);

        result.Should().HaveCount(2);
        result.First().Action.Should().Be("viewed");
    }
}
