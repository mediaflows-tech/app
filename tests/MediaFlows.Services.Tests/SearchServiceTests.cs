using FluentAssertions;
using MediaFlows.Data;
using MediaFlows.Shared.Models.Entities;
using MediaFlows.Shared.Models.Enums;
using MediaFlows.Tests.Common;
using MediaFlows.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Moq;
using Xunit;

namespace MediaFlows.Services.Tests;

public class SearchServiceTests
{
    private TestDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ReplaceService<IModelCacheKeyFactory, TestModelCacheKeyFactory>()
            .Options;
        return new TestDbContext(options);
    }

    [Fact]
    public async Task SearchAsync_EmptyQuery_ReturnsAllPublishedAssets()
    {
        using var db = CreateInMemoryContext();
        db.AppUsers.Add(new AppUser
        {
            CognitoSub = "c1",
            Email = "c@t.com",
            DisplayName = "Creator",
            Role = "ContentCreator"
        });
        db.MediaAssets.AddRange(
            new MediaAsset
            {
                Id = 1,
                Title = "Published",
                CreatorId = "c1",
                Status = AssetStatus.Published,
                S3Key = "k1",
                ContentType = "image/jpeg"
            },
            new MediaAsset
            {
                Id = 2,
                Title = "Draft",
                CreatorId = "c1",
                Status = AssetStatus.Draft,
                S3Key = "k2",
                ContentType = "image/png"
            }
        );
        await db.SaveChangesAsync();

        var service = new SearchService(db);
        var result = await service.SearchAsync("", null, null, 1, 20);

        result.Items.Should().HaveCount(1);
        result.Items.First().Title.Should().Be("Published");
    }

    [Fact]
    public async Task SearchAsync_FiltersByContentType()
    {
        using var db = CreateInMemoryContext();
        db.AppUsers.Add(new AppUser
        {
            CognitoSub = "c1",
            Email = "c@t.com",
            DisplayName = "Creator",
            Role = "ContentCreator"
        });
        db.MediaAssets.AddRange(
            new MediaAsset
            {
                Id = 1,
                Title = "Image",
                CreatorId = "c1",
                Status = AssetStatus.Published,
                S3Key = "k1",
                ContentType = "image/jpeg"
            },
            new MediaAsset
            {
                Id = 2,
                Title = "Video",
                CreatorId = "c1",
                Status = AssetStatus.Published,
                S3Key = "k2",
                ContentType = "video/mp4"
            }
        );
        await db.SaveChangesAsync();

        var service = new SearchService(db);
        var result = await service.SearchAsync("", null, "video", 1, 20);

        result.Items.Should().HaveCount(1);
        result.Items.First().ContentType.Should().StartWith("video");
    }

    [Fact]
    public async Task SearchAsync_PaginatesCorrectly()
    {
        using var db = CreateInMemoryContext();
        db.AppUsers.Add(new AppUser
        {
            CognitoSub = "c1",
            Email = "c@t.com",
            DisplayName = "Creator",
            Role = "ContentCreator"
        });
        for (int i = 1; i <= 25; i++)
        {
            db.MediaAssets.Add(new MediaAsset
            {
                Id = i,
                Title = $"Asset {i}",
                CreatorId = "c1",
                Status = AssetStatus.Published,
                S3Key = $"k{i}",
                ContentType = "image/jpeg"
            });
        }
        await db.SaveChangesAsync();

        var service = new SearchService(db);
        var page1 = await service.SearchAsync("", null, null, 1, 10);
        var page2 = await service.SearchAsync("", null, null, 2, 10);

        page1.Items.Should().HaveCount(10);
        page1.HasMore.Should().BeTrue();
        page1.TotalCount.Should().Be(25);

        page2.Items.Should().HaveCount(10);
        page2.HasMore.Should().BeTrue();
    }

    [Fact]
    public async Task GetAutocompleteSuggestionsAsync_ReturnsEmpty_WhenPrefixTooShort()
    {
        using var db = CreateInMemoryContext();
        var service = new SearchService(db);

        var result = await service.GetAutocompleteSuggestionsAsync("a");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAutocompleteSuggestionsAsync_ReturnsEmpty_WhenPrefixIsWhitespace()
    {
        using var db = CreateInMemoryContext();
        var service = new SearchService(db);

        var result = await service.GetAutocompleteSuggestionsAsync("  ");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAutocompleteSuggestionsAsync_ReturnsEmpty_WhenPrefixIsNull()
    {
        using var db = CreateInMemoryContext();
        var service = new SearchService(db);

        var result = await service.GetAutocompleteSuggestionsAsync(null!);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_FallsBackToTypoTolerantMatching_WhenFullTextMisses()
    {
        using var db = CreateInMemoryContext();
        db.AppUsers.Add(new AppUser
        {
            CognitoSub = "c1",
            Email = "c@t.com",
            DisplayName = "Creator",
            Role = "ContentCreator"
        });
        db.MediaAssets.Add(new MediaAsset
        {
            Id = 1,
            Title = "Sunset Beach",
            Description = "Golden hour coastline",
            CreatorId = "c1",
            Status = AssetStatus.Published,
            S3Key = "k1",
            ContentType = "image/jpeg"
        });
        await db.SaveChangesAsync();

        var service = new SearchService(db);
        var result = await service.SearchAsync("sunste", null, null, 1, 20);

        result.Items.Should().ContainSingle();
        result.Items[0].Title.Should().Be("Sunset Beach");
    }

    [Fact]
    public async Task SearchAsync_UsesPublicPreviewUrl_WhenThumbnailIsMissing()
    {
        using var db = CreateInMemoryContext();
        db.AppUsers.Add(new AppUser
        {
            CognitoSub = "c1",
            Email = "c@t.com",
            DisplayName = "Creator",
            Role = "ContentCreator"
        });
        db.MediaAssets.Add(new MediaAsset
        {
            Id = 1,
            Title = "Preview",
            CreatorId = "c1",
            Status = AssetStatus.Published,
            S3Key = "uploads/1",
            ThumbnailUrl = null,
            ContentType = "image/jpeg"
        });
        await db.SaveChangesAsync();

        var s3 = new Mock<MediaFlows.Shared.Interfaces.IS3StorageService>();
        s3.Setup(x => x.GetPublicUrl("uploads/1"))
            .Returns("https://cdn.example.com/uploads/1");

        var service = new SearchService(db, s3.Object);
        var result = await service.SearchAsync("", null, null, 1, 20);

        result.Items.Should().ContainSingle();
        result.Items[0].PreviewUrl.Should().Be("https://cdn.example.com/uploads/1");
        result.Items[0].ThumbnailUrl.Should().Be("https://cdn.example.com/uploads/1");
    }
}
