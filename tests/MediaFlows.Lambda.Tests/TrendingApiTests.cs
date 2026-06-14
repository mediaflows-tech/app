using System.Text.Json;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestUtilities;
using FluentAssertions;
using MediaFlows.Lambda.TrendingApi;
using MediaFlows.Shared.Models.DynamoDb;
using Moq;

namespace MediaFlows.Lambda.Tests;

public class TrendingApiTests
{
    private readonly Mock<IDynamoDBContext> _dynamoContext = new();
    private readonly Function _sut;
    private readonly TestLambdaContext _ctx = new();

    public TrendingApiTests()
    {
        _sut = new Function(_dynamoContext.Object);
    }

    [Fact]
    public async Task FunctionHandler_ReturnsSortedItemsFromYesterdayBucket()
    {
        var yesterday = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
        var items = new List<TrendingDataItem>
        {
            new() { TimeBucket = yesterday, ScoreAssetId = "000100#1", AssetId = 1, Score = 100, Title = "A" },
            new() { TimeBucket = yesterday, ScoreAssetId = "000500#2", AssetId = 2, Score = 500, Title = "B" },
            new() { TimeBucket = yesterday, ScoreAssetId = "000250#3", AssetId = 3, Score = 250, Title = "C" }
        };
        StubBucket(yesterday, items);

        var response = await _sut.FunctionHandler(new APIGatewayProxyRequest(), _ctx);

        response.StatusCode.Should().Be(200);
        var doc = JsonDocument.Parse(response.Body);
        var scores = doc.RootElement
            .GetProperty("items")
            .EnumerateArray()
            .Select(e => e.GetProperty("score").GetInt32())
            .ToList();
        scores.Should().Equal(500, 250, 100);
        doc.RootElement.GetProperty("bucket").GetString().Should().Be(yesterday);
    }

    [Fact]
    public async Task FunctionHandler_WalksBackUpToSevenDaysWhenRecentBucketsEmpty()
    {
        var yesterday = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
        var twoDaysAgo = DateTime.UtcNow.AddDays(-2).ToString("yyyy-MM-dd");
        StubBucket(yesterday, new List<TrendingDataItem>());
        StubBucket(twoDaysAgo, new List<TrendingDataItem>
        {
            new() { TimeBucket = twoDaysAgo, ScoreAssetId = "000042#7", AssetId = 7, Score = 42, Title = "stale-but-present" }
        });

        var response = await _sut.FunctionHandler(new APIGatewayProxyRequest(), _ctx);

        response.StatusCode.Should().Be(200);
        var doc = JsonDocument.Parse(response.Body);
        doc.RootElement.GetProperty("bucket").GetString().Should().Be(twoDaysAgo);
        doc.RootElement.GetProperty("items").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task FunctionHandler_ReturnsEmptyItemsWhenAllSevenBucketsEmpty()
    {
        for (var offset = 1; offset <= 7; offset++)
        {
            var bucket = DateTime.UtcNow.AddDays(-offset).ToString("yyyy-MM-dd");
            StubBucket(bucket, new List<TrendingDataItem>());
        }

        var response = await _sut.FunctionHandler(new APIGatewayProxyRequest(), _ctx);

        response.StatusCode.Should().Be(200);
        var doc = JsonDocument.Parse(response.Body);
        doc.RootElement.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task FunctionHandler_ReturnsInternalServerErrorWhenDynamoThrows()
    {
        _dynamoContext
            .Setup(x => x.QueryAsync<TrendingDataItem>(It.IsAny<object>()))
            .Throws(new Amazon.DynamoDBv2.AmazonDynamoDBException("simulated DDB outage"));

        var response = await _sut.FunctionHandler(new APIGatewayProxyRequest(), _ctx);

        response.StatusCode.Should().Be(500);
        var doc = JsonDocument.Parse(response.Body);
        doc.RootElement.GetProperty("error").GetString().Should().Be("Internal server error");
    }

    private void StubBucket(string bucket, List<TrendingDataItem> items)
    {
        var search = new Mock<IAsyncSearch<TrendingDataItem>>();
        search.Setup(s => s.GetRemainingAsync(default)).ReturnsAsync(items);
        _dynamoContext.Setup(x => x.QueryAsync<TrendingDataItem>(bucket)).Returns(search.Object);
    }
}
