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

public class BookmarkServiceTests
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
    public async Task ToggleBookmarkAsync_AddsBookmark_WhenNotExists()
    {
        using var db = CreateInMemoryContext();
        db.MediaAssets.Add(new MediaAsset
        {
            Id = 1,
            Title = "Test",
            CreatorId = "c1",
            S3Key = "key",
            ContentType = "image/jpeg",
            Status = AssetStatus.Approved
        });
        await db.SaveChangesAsync();

        var service = new BookmarkService(db);
        var result = await service.ToggleBookmarkAsync("user-1", 1);

        result.Should().BeTrue();
        (await db.Bookmarks.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ToggleBookmarkAsync_RemovesBookmark_WhenExists()
    {
        using var db = CreateInMemoryContext();
        db.Bookmarks.Add(new Bookmark
        {
            UserId = "user-1",
            AssetId = 1,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new BookmarkService(db);
        var result = await service.ToggleBookmarkAsync("user-1", 1);

        result.Should().BeFalse();
        (await db.Bookmarks.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task IsBookmarkedAsync_ReturnsTrue_WhenBookmarkExists()
    {
        using var db = CreateInMemoryContext();
        db.Bookmarks.Add(new Bookmark
        {
            UserId = "user-1",
            AssetId = 1,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new BookmarkService(db);
        var result = await service.IsBookmarkedAsync("user-1", 1);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsBookmarkedAsync_ReturnsFalse_WhenNoBookmark()
    {
        using var db = CreateInMemoryContext();
        var service = new BookmarkService(db);

        var result = await service.IsBookmarkedAsync("user-1", 999);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetUserBookmarksAsync_ReturnsPagedBookmarks()
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
            Title = "Bookmarked",
            CreatorId = "c1",
            S3Key = "key",
            ContentType = "image/jpeg",
            Status = AssetStatus.Approved
        });
        db.Bookmarks.Add(new Bookmark
        {
            UserId = "user-1",
            AssetId = 1,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new BookmarkService(db);
        var result = await service.GetUserBookmarksAsync("user-1", 1, 20);

        result.Items.Should().HaveCount(1);
        result.Items.First().Title.Should().Be("Bookmarked");
    }

    [Fact]
    public async Task GetBookmarkCountAsync_ReturnsCorrectCount()
    {
        using var db = CreateInMemoryContext();
        db.Bookmarks.AddRange(
            new Bookmark { UserId = "user-1", AssetId = 1, CreatedAt = DateTime.UtcNow },
            new Bookmark { UserId = "user-1", AssetId = 2, CreatedAt = DateTime.UtcNow },
            new Bookmark { UserId = "user-2", AssetId = 1, CreatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        var service = new BookmarkService(db);
        var count = await service.GetBookmarkCountAsync("user-1");

        count.Should().Be(2);
    }

    [Fact]
    public async Task GetUserBookmarksAsync_UsesPreviewFallback_WhenThumbnailIsMissing()
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
            Title = "Bookmarked",
            CreatorId = "c1",
            S3Key = "uploads/c1/bookmarked.jpg",
            ThumbnailUrl = null,
            ContentType = "image/jpeg",
            Status = AssetStatus.Approved
        });
        db.Bookmarks.Add(new Bookmark
        {
            UserId = "user-1",
            AssetId = 1,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var s3 = new Mock<MediaFlows.Shared.Interfaces.IS3StorageService>();
        s3.Setup(x => x.GetPublicUrl("uploads/c1/bookmarked.jpg"))
            .Returns("https://cdn.example.com/uploads/c1/bookmarked.jpg");

        var service = new BookmarkService(db, s3.Object);
        var result = await service.GetUserBookmarksAsync("user-1", 1, 20);

        result.Items.Should().ContainSingle();
        result.Items[0].PreviewUrl.Should().Be("https://cdn.example.com/uploads/c1/bookmarked.jpg");
        result.Items[0].ThumbnailUrl.Should().Be("https://cdn.example.com/uploads/c1/bookmarked.jpg");
    }
}
