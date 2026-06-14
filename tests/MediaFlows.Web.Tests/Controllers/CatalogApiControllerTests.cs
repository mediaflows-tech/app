using FluentAssertions;
using MediaFlows.Shared.DTOs;
using MediaFlows.Shared.Interfaces;
using MediaFlows.Shared.Models.DynamoDb;
using MediaFlows.Shared.Models.Enums;
using MediaFlows.Shared.Models.ValueObjects;
using MediaFlows.Web.Controllers.Api;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace MediaFlows.Web.Tests.Controllers;

public class CatalogApiControllerTests
{
    private readonly Mock<IMediaAssetService> _assetService = new();
    private readonly Mock<IDynamoDbService> _dynamoDb = new();
    private readonly Mock<IBookmarkService> _bookmarks = new();
    private readonly Mock<ICommentService> _comments = new();
    private readonly Mock<IS3StorageService> _s3 = new();

    private CatalogApiController BuildSut() =>
        new(_assetService.Object, _dynamoDb.Object, _bookmarks.Object, _comments.Object, _s3.Object);

    private static MediaAssetSummaryDto Summary(int id, string title = "asset") =>
        new()
        {
            Id = id,
            Title = title,
            ContentType = "image/jpeg",
            FileSize = 100,
            Status = AssetStatus.Published,
            CreatorName = "creator",
            CreatedAt = DateTime.UtcNow
        };

    private static TrendingDataItem Trending(int assetId, int score) =>
        new()
        {
            TimeBucket = "ignored",
            ScoreAssetId = $"{score:D6}#{assetId}",
            AssetId = assetId,
            Score = score,
            Title = $"Asset {assetId}",
            ThumbnailUrl = ""
        };

    [Fact]
    public async Task GetCatalog_SortNewest_DelegatesToGetPagedAssetsAsync()
    {
        var paged = new PagedResult<MediaAssetSummaryDto>
        {
            Items = new List<MediaAssetSummaryDto> { Summary(1) },
            TotalCount = 1, Page = 1, PageSize = 20, HasMore = false
        };
        _assetService.Setup(a => a.GetPagedAssetsAsync(
            null, AssetStatus.Published, 1, 20, null, null))
            .ReturnsAsync(paged);

        var result = await BuildSut().GetCatalog(page: 1, type: null, sort: null);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(paged);
        _dynamoDb.Verify(d => d.GetTrendingAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetCatalog_SortValue_PassedThroughToService()
    {
        _assetService.Setup(a => a.GetPagedAssetsAsync(
            null, AssetStatus.Published, 1, 20, "image", "oldest"))
            .ReturnsAsync(new PagedResult<MediaAssetSummaryDto>
            {
                Items = new List<MediaAssetSummaryDto>(),
                TotalCount = 0, Page = 1, PageSize = 20, HasMore = false
            });

        await BuildSut().GetCatalog(page: 1, type: "image", sort: "oldest");

        _assetService.Verify(a => a.GetPagedAssetsAsync(
            null, AssetStatus.Published, 1, 20, "image", "oldest"), Times.Once);
    }

    [Fact]
    public async Task GetCatalog_SortTrending_ReturnsAssetsInScoreOrder()
    {
        // Trending list (top to bottom): id 3 (score 50), id 1 (score 30), id 2 (score 10)
        var trending = new List<TrendingDataItem>
        {
            Trending(3, 50),
            Trending(1, 30),
            Trending(2, 10)
        };
        _dynamoDb.Setup(d => d.GetTrendingAsync(It.IsAny<string>(), 50))
            .ReturnsAsync(trending);

        // Service returns IDs in arbitrary order — controller must reorder
        _assetService.Setup(a => a.GetByIdsAsync(
            It.Is<IReadOnlyList<int>>(ids => ids.SequenceEqual(new[] { 3, 1, 2 })), null))
            .ReturnsAsync(new List<MediaAssetSummaryDto>
            {
                Summary(1, "one"),
                Summary(2, "two"),
                Summary(3, "three")
            });

        var result = await BuildSut().GetCatalog(page: 1, type: null, sort: "trending");

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var paged = ok.Value.Should().BeOfType<PagedResult<MediaAssetSummaryDto>>().Subject;
        paged.Items.Select(i => i.Id).Should().Equal(3, 1, 2);
        paged.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task GetCatalog_SortTrending_FallsBackToOlderDateWhenYesterdayEmpty()
    {
        var twoDaysAgo = DateTime.UtcNow.AddDays(-2).ToString("yyyy-MM-dd");

        // Catch-all: anything other than 2-days-ago is empty (including yesterday)
        _dynamoDb.Setup(d => d.GetTrendingAsync(It.IsAny<string>(), 50))
            .ReturnsAsync(new List<TrendingDataItem>());
        _dynamoDb.Setup(d => d.GetTrendingAsync(twoDaysAgo, 50))
            .ReturnsAsync(new List<TrendingDataItem> { Trending(7, 5) });

        _assetService.Setup(a => a.GetByIdsAsync(
            It.Is<IReadOnlyList<int>>(ids => ids.SequenceEqual(new[] { 7 })), null))
            .ReturnsAsync(new List<MediaAssetSummaryDto> { Summary(7) });

        var result = await BuildSut().GetCatalog(page: 1, type: null, sort: "trending");

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var paged = ok.Value.Should().BeOfType<PagedResult<MediaAssetSummaryDto>>().Subject;
        paged.Items.Should().ContainSingle().Which.Id.Should().Be(7);
    }

    [Fact]
    public async Task GetCatalog_SortTrending_EmptyAcrossAllDays_ReturnsEmptyPagedResult()
    {
        _dynamoDb.Setup(d => d.GetTrendingAsync(It.IsAny<string>(), 50))
            .ReturnsAsync(new List<TrendingDataItem>());

        var result = await BuildSut().GetCatalog(page: 1, type: null, sort: "trending");

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var paged = ok.Value.Should().BeOfType<PagedResult<MediaAssetSummaryDto>>().Subject;
        paged.Items.Should().BeEmpty();
        paged.HasMore.Should().BeFalse();
        _assetService.Verify(a => a.GetByIdsAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetCatalog_SortTrending_PassesTypeFilterToService()
    {
        _dynamoDb.Setup(d => d.GetTrendingAsync(It.IsAny<string>(), 50))
            .ReturnsAsync(new List<TrendingDataItem> { Trending(1, 10) });

        _assetService.Setup(a => a.GetByIdsAsync(It.IsAny<IReadOnlyList<int>>(), "video"))
            .ReturnsAsync(new List<MediaAssetSummaryDto>());

        await BuildSut().GetCatalog(page: 1, type: "video", sort: "trending");

        _assetService.Verify(a => a.GetByIdsAsync(It.IsAny<IReadOnlyList<int>>(), "video"), Times.Once);
    }

    [Fact]
    public async Task GetCatalog_SortTrending_DropsIdsServiceDidNotReturn()
    {
        // DynamoDB has 3 trending IDs, but service only returns 2 (third is unpublished/deleted)
        _dynamoDb.Setup(d => d.GetTrendingAsync(It.IsAny<string>(), 50))
            .ReturnsAsync(new List<TrendingDataItem>
            {
                Trending(1, 30),
                Trending(2, 20),
                Trending(3, 10)
            });

        _assetService.Setup(a => a.GetByIdsAsync(It.IsAny<IReadOnlyList<int>>(), null))
            .ReturnsAsync(new List<MediaAssetSummaryDto>
            {
                Summary(1),
                Summary(3) // id 2 missing
            });

        var result = await BuildSut().GetCatalog(page: 1, type: null, sort: "trending");

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var paged = ok.Value.Should().BeOfType<PagedResult<MediaAssetSummaryDto>>().Subject;
        paged.Items.Select(i => i.Id).Should().Equal(1, 3);
    }

    [Fact]
    public async Task GetCatalog_SortTrending_IsCaseInsensitive()
    {
        _dynamoDb.Setup(d => d.GetTrendingAsync(It.IsAny<string>(), 50))
            .ReturnsAsync(new List<TrendingDataItem>());

        await BuildSut().GetCatalog(page: 1, type: null, sort: "TRENDING");

        _dynamoDb.Verify(d => d.GetTrendingAsync(It.IsAny<string>(), 50), Times.AtLeastOnce);
    }
}
