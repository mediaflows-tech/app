using FluentAssertions;
using MediaFlows.Data;
using MediaFlows.Shared.Interfaces;
using MediaFlows.Shared.Models.Entities;
using MediaFlows.Shared.Models.Enums;
using MediaFlows.Tests.Common;
using MediaFlows.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using Moq;

namespace MediaFlows.Services.Tests;

public class MediaAssetServiceTests : IDisposable
{
    private readonly TestDbContext _db;
    private readonly Mock<IAuditLogService> _auditLog;
    private readonly Mock<IS3StorageService> _s3Storage;
    private readonly MediaAssetService _sut;

    public MediaAssetServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ReplaceService<IModelCacheKeyFactory, TestModelCacheKeyFactory>()
            .Options;
        _db = new TestDbContext(options);

        _db.AppUsers.Add(new AppUser
        {
            CognitoSub = "creator-1",
            Email = "creator@test.com",
            DisplayName = "Creator",
            Role = "ContentCreator",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        });
        _db.SaveChanges();

        _auditLog = new Mock<IAuditLogService>();
        _s3Storage = new Mock<IS3StorageService>();
        var logger = new Mock<ILogger<MediaAssetService>>();
        _sut = new MediaAssetService(_db, _auditLog.Object, _s3Storage.Object, logger.Object);
    }

    [Fact]
    public async Task CreateAsync_AddsAssetAndLogsAudit()
    {
        var asset = new MediaAsset
        {
            CreatorId = "creator-1",
            Title = "test.jpg",
            S3Key = "uploads/creator-1/uuid/test.jpg",
            ContentType = "image/jpeg",
            FileSize = 1024
        };

        var result = await _sut.CreateAsync(asset);

        result.Id.Should().BeGreaterThan(0);
        _auditLog.Verify(x => x.LogAsync("MediaAsset.Create", "MediaAsset",
            It.IsAny<string>(), It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_IncludesRelatedEntities()
    {
        var asset = new MediaAsset
        {
            CreatorId = "creator-1",
            Title = "full.jpg",
            S3Key = "k1",
            ContentType = "image/jpeg",
            FileSize = 100
        };
        _db.MediaAssets.Add(asset);
        await _db.SaveChangesAsync();

        var result = await _sut.GetByIdAsync(asset.Id);

        result.Should().NotBeNull();
        result!.Creator.Should().NotBeNull();
        result.Title.Should().Be("full.jpg");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullWhenNotFound()
    {
        var result = await _sut.GetByIdAsync(999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_SetsId()
    {
        var asset1 = new MediaAsset { CreatorId = "creator-1", Title = "a.jpg", S3Key = "k1", ContentType = "image/jpeg", FileSize = 100 };
        var asset2 = new MediaAsset { CreatorId = "creator-1", Title = "b.jpg", S3Key = "k2", ContentType = "image/jpeg", FileSize = 200 };

        var r1 = await _sut.CreateAsync(asset1);
        var r2 = await _sut.CreateAsync(asset2);

        r1.Id.Should().NotBe(r2.Id);
        r1.Id.Should().BeGreaterThan(0);
        r2.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ServiceImplementsInterface()
    {
        typeof(MediaAssetService).Should().Implement<IMediaAssetService>();
    }

    [Fact]
    public async Task GetByIdAsync_IncludesVersions()
    {
        var asset = new MediaAsset
        {
            CreatorId = "creator-1",
            Title = "versioned.jpg",
            S3Key = "k1",
            ContentType = "image/jpeg",
            FileSize = 100
        };
        _db.MediaAssets.Add(asset);
        await _db.SaveChangesAsync();

        _db.AssetVersions.Add(new AssetVersion
        {
            AssetId = asset.Id,
            VersionNumber = 1,
            S3Key = "v1/k1",
            ContentType = "image/jpeg",
            FileSize = 100,
            UploadedById = "creator-1",
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var result = await _sut.GetByIdAsync(asset.Id);

        result!.Versions.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetPagedAssetsAsync_UsesOriginalImageAsPreview_WhenThumbnailIsMissing()
    {
        _db.MediaAssets.Add(new MediaAsset
        {
            CreatorId = "creator-1",
            Title = "preview.jpg",
            S3Key = "uploads/creator-1/preview.jpg",
            ContentType = "image/jpeg",
            FileSize = 1234,
            ThumbnailUrl = null,
            Status = AssetStatus.Draft
        });
        await _db.SaveChangesAsync();

        _s3Storage
            .Setup(x => x.GetPublicUrl("uploads/creator-1/preview.jpg"))
            .Returns("https://cdn.example.com/uploads/creator-1/preview.jpg");

        var result = await _sut.GetPagedAssetsAsync("creator-1", null, 1, 20);

        result.Items.Should().ContainSingle();
        result.Items[0].PreviewUrl.Should().Be("https://cdn.example.com/uploads/creator-1/preview.jpg");
        result.Items[0].ThumbnailUrl.Should().Be("https://cdn.example.com/uploads/creator-1/preview.jpg");
    }

    [Fact]
    public async Task DeleteAsync_SetsIsDeleted_AndRemovesAssetFromPagedResults()
    {
        var asset = new MediaAsset
        {
            CreatorId = "creator-1",
            Title = "delete-me.jpg",
            S3Key = "uploads/creator-1/delete-me.jpg",
            ContentType = "image/jpeg",
            FileSize = 321,
            Status = AssetStatus.Draft
        };
        _db.MediaAssets.Add(asset);
        await _db.SaveChangesAsync();

        await _sut.DeleteAsync(asset.Id, "creator-1");

        var deletedAsset = await _db.MediaAssets.IgnoreQueryFilters().FirstAsync(a => a.Id == asset.Id);
        deletedAsset.IsDeleted.Should().BeTrue();
        deletedAsset.Status.Should().Be(AssetStatus.Deleted);

        var result = await _sut.GetPagedAssetsAsync("creator-1", null, 1, 20);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByIdsAsync_ReturnsOnlyPublishedAssets()
    {
        var published = new MediaAsset
        {
            CreatorId = "creator-1", Title = "pub.jpg", S3Key = "k1",
            ContentType = "image/jpeg", FileSize = 100, Status = AssetStatus.Published
        };
        var draft = new MediaAsset
        {
            CreatorId = "creator-1", Title = "draft.jpg", S3Key = "k2",
            ContentType = "image/jpeg", FileSize = 100, Status = AssetStatus.Draft
        };
        _db.MediaAssets.AddRange(published, draft);
        await _db.SaveChangesAsync();

        var result = await _sut.GetByIdsAsync(new[] { published.Id, draft.Id });

        result.Should().ContainSingle();
        result[0].Id.Should().Be(published.Id);
    }

    [Fact]
    public async Task GetByIdsAsync_FiltersByFileTypePrefix()
    {
        var image = new MediaAsset
        {
            CreatorId = "creator-1", Title = "i.jpg", S3Key = "k1",
            ContentType = "image/jpeg", FileSize = 100, Status = AssetStatus.Published
        };
        var video = new MediaAsset
        {
            CreatorId = "creator-1", Title = "v.mp4", S3Key = "k2",
            ContentType = "video/mp4", FileSize = 100, Status = AssetStatus.Published
        };
        _db.MediaAssets.AddRange(image, video);
        await _db.SaveChangesAsync();

        var result = await _sut.GetByIdsAsync(new[] { image.Id, video.Id }, fileType: "video");

        result.Should().ContainSingle();
        result[0].Id.Should().Be(video.Id);
    }

    [Fact]
    public async Task GetByIdsAsync_SilentlyDropsMissingIds()
    {
        var asset = new MediaAsset
        {
            CreatorId = "creator-1", Title = "real.jpg", S3Key = "k1",
            ContentType = "image/jpeg", FileSize = 100, Status = AssetStatus.Published
        };
        _db.MediaAssets.Add(asset);
        await _db.SaveChangesAsync();

        var result = await _sut.GetByIdsAsync(new[] { asset.Id, 99_999 });

        result.Should().ContainSingle();
        result[0].Id.Should().Be(asset.Id);
    }

    [Fact]
    public async Task GetByIdsAsync_EnrichesPreviewUrlForImagesWithoutThumbnail()
    {
        var asset = new MediaAsset
        {
            CreatorId = "creator-1", Title = "preview.jpg",
            S3Key = "uploads/creator-1/preview.jpg",
            ContentType = "image/jpeg", FileSize = 100,
            ThumbnailUrl = null, Status = AssetStatus.Published
        };
        _db.MediaAssets.Add(asset);
        await _db.SaveChangesAsync();

        _s3Storage.Setup(x => x.GetPublicUrl("uploads/creator-1/preview.jpg"))
            .Returns("https://cdn.example.com/preview.jpg");

        var result = await _sut.GetByIdsAsync(new[] { asset.Id });

        result.Should().ContainSingle();
        result[0].PreviewUrl.Should().Be("https://cdn.example.com/preview.jpg");
        result[0].ThumbnailUrl.Should().Be("https://cdn.example.com/preview.jpg");
    }

    [Fact]
    public async Task GetByIdsAsync_EmptyIdList_ReturnsEmpty()
    {
        var result = await _sut.GetByIdsAsync(Array.Empty<int>());

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_RemovesDependentRows()
    {
        var asset = new MediaAsset
        {
            CreatorId = "creator-1",
            Title = "with-children.jpg",
            S3Key = "k1",
            ContentType = "image/jpeg",
            FileSize = 100,
            Status = AssetStatus.Published
        };
        _db.MediaAssets.Add(asset);
        await _db.SaveChangesAsync();

        var version = new AssetVersion
        {
            AssetId = asset.Id,
            VersionNumber = 1,
            S3Key = "v1/k1",
            ContentType = "image/jpeg",
            FileSize = 100,
            UploadedById = "creator-1"
        };
        _db.AssetVersions.Add(version);
        _db.Comments.Add(new Comment { AssetId = asset.Id, AuthorId = "creator-1", Content = "Nice asset" });
        _db.Reviews.Add(new Review
        {
            AssetId = asset.Id,
            ReviewerId = "creator-1",
            Decision = ReviewDecision.Approved,
            ReviewedAt = DateTime.UtcNow
        });
        _db.Bookmarks.Add(new Bookmark { UserId = "creator-1", AssetId = asset.Id });
        await _db.SaveChangesAsync();

        asset.CurrentVersionId = version.Id;
        await _db.SaveChangesAsync();

        await _sut.DeleteAsync(asset.Id, "creator-1");

        (await _db.Comments.CountAsync()).Should().Be(0);
        (await _db.Reviews.CountAsync()).Should().Be(0);
        (await _db.Bookmarks.CountAsync()).Should().Be(0);
        (await _db.AssetVersions.CountAsync()).Should().Be(0);
        asset.CurrentVersionId.Should().BeNull();
    }

    public void Dispose() => _db.Dispose();
}
