using FluentAssertions;
using MediaFlows.Data;
using MediaFlows.Shared.DTOs;
using MediaFlows.Shared.Interfaces;
using MediaFlows.Shared.Models.Entities;
using MediaFlows.Shared.Models.Enums;
using MediaFlows.Tests.Common;
using MediaFlows.Web.Hubs;
using MediaFlows.Web.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Moq;
using Xunit;

namespace MediaFlows.Services.Tests;

public class ReviewServiceTests
{
    private TestDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ReplaceService<IModelCacheKeyFactory, TestModelCacheKeyFactory>()
            .Options;
        return new TestDbContext(options);
    }

    // SQLite in-memory — a real relational provider, needed for tests that
    // exercise ExecuteUpdate (the EF InMemory provider cannot translate it).
    private static TestDbContext CreateSqliteContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .ReplaceService<IModelCacheKeyFactory, TestModelCacheKeyFactory>()
            .Options;
        var db = new TestDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private ReviewService CreateService(
        ApplicationDbContext db,
        Mock<IHubContext<NotificationHub, INotificationClient>>? hubMock = null,
        Mock<INotificationService>? notifMock = null,
        Mock<IAuditLogService>? auditMock = null,
        Mock<IReviewEventPublisher>? eventPublisherMock = null)
    {
        hubMock ??= new Mock<IHubContext<NotificationHub, INotificationClient>>();
        notifMock ??= new Mock<INotificationService>();
        auditMock ??= new Mock<IAuditLogService>();
        eventPublisherMock ??= new Mock<IReviewEventPublisher>();

        var clientMock = new Mock<INotificationClient>();
        hubMock.Setup(h => h.Clients.Group(It.IsAny<string>())).Returns(clientMock.Object);
        hubMock.Setup(h => h.Clients.Groups(It.IsAny<IReadOnlyList<string>>())).Returns(clientMock.Object);

        var s3Mock = new Mock<IS3StorageService>();
        s3Mock.Setup(s => s.GetPublicUrl(It.IsAny<string>()))
              .Returns((string key) => $"https://cdn.example.com/{key}");

        return new ReviewService(db, hubMock.Object, notifMock.Object, auditMock.Object, s3Mock.Object, eventPublisherMock.Object);
    }

    [Fact]
    public async Task GetPendingReviewsAsync_ReturnsOnlyPendingReviewAssets()
    {
        using var db = CreateInMemoryContext();
        db.AppUsers.Add(new AppUser
        {
            CognitoSub = "user-1",
            Email = "test@test.com",
            DisplayName = "Creator",
            Role = "ContentCreator"
        });
        db.MediaAssets.AddRange(
            new MediaAsset
            {
                Id = 1,
                Title = "Pending",
                CreatorId = "user-1",
                Status = AssetStatus.PendingReview,
                S3Key = "k1",
                ContentType = "image/jpeg"
            },
            new MediaAsset
            {
                Id = 2,
                Title = "Draft",
                CreatorId = "user-1",
                Status = AssetStatus.Draft,
                S3Key = "k2",
                ContentType = "image/png"
            },
            new MediaAsset
            {
                Id = 3,
                Title = "Submitted",
                CreatorId = "user-1",
                Status = AssetStatus.Submitted,
                S3Key = "k3",
                ContentType = "video/mp4"
            }
        );
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.GetPendingReviewsAsync(null, null, null, null, null, 1, 20);

        result.Items.Should().HaveCount(2);
        result.Items.Should().OnlyContain(i =>
            i.Status == AssetStatus.PendingReview || i.Status == AssetStatus.Submitted);
    }

    [Fact]
    public async Task GetPendingReviewsAsync_FiltersByStatus()
    {
        using var db = CreateInMemoryContext();
        db.AppUsers.Add(new AppUser
        {
            CognitoSub = "user-1",
            Email = "test@test.com",
            DisplayName = "Creator",
            Role = "ContentCreator"
        });
        db.MediaAssets.AddRange(
            new MediaAsset
            {
                Id = 1,
                Title = "PendingReview",
                CreatorId = "user-1",
                Status = AssetStatus.PendingReview,
                S3Key = "k1",
                ContentType = "image/jpeg"
            },
            new MediaAsset
            {
                Id = 2,
                Title = "Submitted",
                CreatorId = "user-1",
                Status = AssetStatus.Submitted,
                S3Key = "k2",
                ContentType = "image/png"
            }
        );
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.GetPendingReviewsAsync(
            AssetStatus.PendingReview, null, null, null, null, 1, 20);

        result.Items.Should().HaveCount(1);
        result.Items.First().Status.Should().Be(AssetStatus.PendingReview);
    }

    [Fact]
    public async Task GetPendingReviewsAsync_SortsByDateDescByDefault()
    {
        using var db = CreateInMemoryContext();
        db.AppUsers.Add(new AppUser
        {
            CognitoSub = "user-1",
            Email = "test@test.com",
            DisplayName = "Creator",
            Role = "ContentCreator"
        });
        await db.SaveChangesAsync();

        // Add assets separately so SaveChangesAsync doesn't override CreatedAt to the same instant
        var older = new MediaAsset
        {
            Id = 1,
            Title = "Older",
            CreatorId = "user-1",
            Status = AssetStatus.PendingReview,
            S3Key = "k1",
            ContentType = "image/jpeg"
        };
        db.MediaAssets.Add(older);
        await db.SaveChangesAsync();
        // Manually set CreatedAt after save to bypass the auto-timestamp
        older.CreatedAt = DateTime.UtcNow.AddDays(-2);
        await db.SaveChangesAsync();

        var newer = new MediaAsset
        {
            Id = 2,
            Title = "Newer",
            CreatorId = "user-1",
            Status = AssetStatus.PendingReview,
            S3Key = "k2",
            ContentType = "image/png"
        };
        db.MediaAssets.Add(newer);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.GetPendingReviewsAsync(null, null, null, null, null, 1, 20);

        result.Items.First().Title.Should().Be("Newer");
    }

    [Fact]
    public async Task SubmitDecisionAsync_ApprovesAsset_AndNotifiesCreator()
    {
        using var db = CreateInMemoryContext();
        db.AppUsers.Add(new AppUser
        {
            CognitoSub = "creator-1",
            Email = "c@test.com",
            DisplayName = "Creator",
            Role = "ContentCreator"
        });
        db.MediaAssets.Add(new MediaAsset
        {
            Id = 1,
            Title = "Test Asset",
            CreatorId = "creator-1",
            Status = AssetStatus.PendingReview,
            S3Key = "key",
            ContentType = "image/jpeg"
        });
        await db.SaveChangesAsync();

        var hubMock = new Mock<IHubContext<NotificationHub, INotificationClient>>();
        var clientMock = new Mock<INotificationClient>();
        hubMock.Setup(h => h.Clients.Group(It.IsAny<string>())).Returns(clientMock.Object);
        hubMock.Setup(h => h.Clients.Groups(It.IsAny<IReadOnlyList<string>>())).Returns(clientMock.Object);
        var notifMock = new Mock<INotificationService>();
        var auditMock = new Mock<IAuditLogService>();

        var s3Mock = new Mock<IS3StorageService>();
        var service = new ReviewService(db, hubMock.Object, notifMock.Object, auditMock.Object, s3Mock.Object, new Mock<IReviewEventPublisher>().Object);
        await service.SubmitDecisionAsync(1, ReviewDecision.Approved, "reviewer-1", "Looks great!");

        var asset = await db.MediaAssets.FindAsync(1);
        asset!.Status.Should().Be(AssetStatus.Approved);

        var review = await db.Reviews.FirstAsync();
        review.Decision.Should().Be(ReviewDecision.Approved);
        review.ReviewerId.Should().Be("reviewer-1");
        review.Comments.Should().Be("Looks great!");

        clientMock.Verify(c => c.ReceiveToast(
            It.Is<string>(s => s.Contains("Approved")),
            It.Is<string>(s => s.Contains("Test Asset")),
            "success"), Times.Once);
    }

    [Fact]
    public async Task SubmitDecisionAsync_RejectsWithoutComments_ThrowsArgumentException()
    {
        using var db = CreateInMemoryContext();
        db.MediaAssets.Add(new MediaAsset
        {
            Id = 1,
            Title = "Test",
            CreatorId = "user-1",
            Status = AssetStatus.PendingReview,
            S3Key = "key",
            ContentType = "image/jpeg"
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var act = () => service.SubmitDecisionAsync(1, ReviewDecision.Rejected, "reviewer-1", null);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Comments are required*");
    }

    [Fact]
    public async Task SubmitDecisionAsync_OnDraftAsset_ThrowsInvalidOperationException()
    {
        using var db = CreateInMemoryContext();
        db.MediaAssets.Add(new MediaAsset
        {
            Id = 1,
            Title = "Test",
            CreatorId = "user-1",
            Status = AssetStatus.Draft,
            S3Key = "key",
            ContentType = "image/jpeg"
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var act = () => service.SubmitDecisionAsync(1, ReviewDecision.Approved, "reviewer-1", null);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*must be PendingReview or Submitted*");
    }

    [Fact]
    public async Task BatchDecisionAsync_ApprovesMultipleAssets()
    {
        using var db = CreateInMemoryContext();
        db.AppUsers.Add(new AppUser
        {
            CognitoSub = "user-1",
            Email = "test@test.com",
            DisplayName = "Creator",
            Role = "ContentCreator"
        });
        db.MediaAssets.AddRange(
            new MediaAsset
            {
                Id = 1,
                Title = "A1",
                CreatorId = "user-1",
                Status = AssetStatus.PendingReview,
                S3Key = "k1",
                ContentType = "image/jpeg"
            },
            new MediaAsset
            {
                Id = 2,
                Title = "A2",
                CreatorId = "user-1",
                Status = AssetStatus.PendingReview,
                S3Key = "k2",
                ContentType = "image/png"
            }
        );
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.BatchDecisionAsync(new[] { 1, 2 }, ReviewDecision.Approved, "reviewer-1", "All good");

        var assets = await db.MediaAssets.ToListAsync();
        assets.Should().OnlyContain(a => a.Status == AssetStatus.Approved);
    }

    [Fact]
    public async Task SchedulePublishAsync_SetsScheduleDate_ForApprovedAsset()
    {
        using var db = CreateInMemoryContext();
        var futureDate = DateTime.UtcNow.AddDays(7);
        db.MediaAssets.Add(new MediaAsset
        {
            Id = 1,
            Title = "Test",
            CreatorId = "user-1",
            Status = AssetStatus.Approved,
            S3Key = "key",
            ContentType = "image/jpeg"
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.SchedulePublishAsync(1, futureDate, "editor-1");

        var asset = await db.MediaAssets.FindAsync(1);
        asset!.ScheduledPublishAt.Should().Be(futureDate);
    }

    [Fact]
    public async Task SchedulePublishAsync_OnNonApprovedAsset_Throws()
    {
        using var db = CreateInMemoryContext();
        db.MediaAssets.Add(new MediaAsset
        {
            Id = 1,
            Title = "Test",
            CreatorId = "user-1",
            Status = AssetStatus.Draft,
            S3Key = "key",
            ContentType = "image/jpeg"
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var act = () => service.SchedulePublishAsync(1, DateTime.UtcNow.AddDays(1), "editor-1");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*approved*");
    }

    [Fact]
    public async Task GetPendingCountAsync_ReturnsPendingAndSubmittedCount()
    {
        using var db = CreateInMemoryContext();
        db.MediaAssets.AddRange(
            new MediaAsset
            {
                Id = 1,
                Title = "P",
                CreatorId = "u1",
                Status = AssetStatus.PendingReview,
                S3Key = "k1",
                ContentType = "image/jpeg"
            },
            new MediaAsset
            {
                Id = 2,
                Title = "S",
                CreatorId = "u1",
                Status = AssetStatus.Submitted,
                S3Key = "k2",
                ContentType = "image/jpeg"
            },
            new MediaAsset
            {
                Id = 3,
                Title = "A",
                CreatorId = "u1",
                Status = AssetStatus.Approved,
                S3Key = "k3",
                ContentType = "image/jpeg"
            }
        );
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var count = await service.GetPendingCountAsync();

        count.Should().Be(2);
    }

    [Fact]
    public async Task GetApprovedCountAsync_ReturnsApprovedCount()
    {
        using var db = CreateInMemoryContext();
        db.MediaAssets.AddRange(
            new MediaAsset
            {
                Id = 1,
                Title = "A1",
                CreatorId = "u1",
                Status = AssetStatus.Approved,
                S3Key = "k1",
                ContentType = "image/jpeg"
            },
            new MediaAsset
            {
                Id = 2,
                Title = "A2",
                CreatorId = "u1",
                Status = AssetStatus.Approved,
                S3Key = "k2",
                ContentType = "image/jpeg"
            },
            new MediaAsset
            {
                Id = 3,
                Title = "D",
                CreatorId = "u1",
                Status = AssetStatus.Draft,
                S3Key = "k3",
                ContentType = "image/jpeg"
            }
        );
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var count = await service.GetApprovedCountAsync();

        count.Should().Be(2);
    }

    [Fact]
    public async Task GetRejectedCountAsync_ReturnsRejectedCount()
    {
        using var db = CreateInMemoryContext();
        db.MediaAssets.AddRange(
            new MediaAsset
            {
                Id = 1,
                Title = "R",
                CreatorId = "u1",
                Status = AssetStatus.Rejected,
                S3Key = "k1",
                ContentType = "image/jpeg"
            },
            new MediaAsset
            {
                Id = 2,
                Title = "A",
                CreatorId = "u1",
                Status = AssetStatus.Approved,
                S3Key = "k2",
                ContentType = "image/jpeg"
            }
        );
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var count = await service.GetRejectedCountAsync();

        count.Should().Be(1);
    }

    [Fact]
    public async Task GetReviewDetailsAsync_ReturnsAssetWithReviewHistory()
    {
        using var db = CreateInMemoryContext();
        db.AppUsers.AddRange(
            new AppUser { CognitoSub = "c1", Email = "c@t.com", DisplayName = "Creator", Role = "ContentCreator" },
            new AppUser { CognitoSub = "r1", Email = "r@t.com", DisplayName = "Reviewer", Role = "Editor" }
        );
        db.MediaAssets.Add(new MediaAsset
        {
            Id = 1,
            Title = "Review Me",
            CreatorId = "c1",
            Status = AssetStatus.Approved,
            S3Key = "key",
            ContentType = "image/jpeg"
        });
        db.Reviews.Add(new Review
        {
            AssetId = 1,
            ReviewerId = "r1",
            Decision = ReviewDecision.Approved,
            Comments = "LGTM",
            ReviewedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.GetReviewDetailsAsync(1);

        result.Should().NotBeNull();
        result!.Title.Should().Be("Review Me");
        result.ReviewHistory.Should().HaveCount(1);
        result.ReviewHistory.First().Decision.Should().Be(ReviewDecision.Approved);
    }

    [Fact]
    public async Task GetReviewDetailsAsync_ReturnsNull_WhenNotFound()
    {
        using var db = CreateInMemoryContext();
        var service = CreateService(db);

        var result = await service.GetReviewDetailsAsync(999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetScheduledAssetsAsync_ReturnsAssetsWithinDateRange()
    {
        using var db = CreateInMemoryContext();
        var start = DateTime.UtcNow;
        var end = DateTime.UtcNow.AddDays(7);

        db.MediaAssets.AddRange(
            new MediaAsset
            {
                Id = 1,
                Title = "Scheduled",
                CreatorId = "u1",
                Status = AssetStatus.Approved,
                S3Key = "k1",
                ContentType = "image/jpeg",
                ScheduledPublishAt = DateTime.UtcNow.AddDays(3)
            },
            new MediaAsset
            {
                Id = 2,
                Title = "Not Scheduled",
                CreatorId = "u1",
                Status = AssetStatus.Approved,
                S3Key = "k2",
                ContentType = "image/jpeg",
                ScheduledPublishAt = null
            },
            new MediaAsset
            {
                Id = 3,
                Title = "Out of Range",
                CreatorId = "u1",
                Status = AssetStatus.Approved,
                S3Key = "k3",
                ContentType = "image/jpeg",
                ScheduledPublishAt = DateTime.UtcNow.AddDays(30)
            }
        );
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.GetScheduledAssetsAsync(start, end);

        result.Should().HaveCount(1);
        result.First().Title.Should().Be("Scheduled");
    }

    [Fact]
    public async Task ReschedulePublishAsync_UpdatesScheduleDate()
    {
        using var db = CreateInMemoryContext();
        var oldDate = DateTime.UtcNow.AddDays(3);
        var newDate = DateTime.UtcNow.AddDays(5);
        db.MediaAssets.Add(new MediaAsset
        {
            Id = 1,
            Title = "Reschedule Me",
            CreatorId = "u1",
            Status = AssetStatus.Approved,
            S3Key = "k1",
            ContentType = "image/jpeg",
            ScheduledPublishAt = oldDate
        });
        await db.SaveChangesAsync();

        var auditMock = new Mock<IAuditLogService>();
        var service = CreateService(db, auditMock: auditMock);
        await service.ReschedulePublishAsync(1, newDate, "editor-1");

        var asset = await db.MediaAssets.FindAsync(1);
        asset!.ScheduledPublishAt.Should().Be(newDate);
        auditMock.Verify(a => a.LogAsync("Schedule.Updated", "MediaAsset", "1", It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task ReschedulePublishAsync_ThrowsForPastDate()
    {
        using var db = CreateInMemoryContext();
        db.MediaAssets.Add(new MediaAsset
        {
            Id = 1,
            Title = "Test",
            CreatorId = "u1",
            Status = AssetStatus.Approved,
            S3Key = "k1",
            ContentType = "image/jpeg",
            ScheduledPublishAt = DateTime.UtcNow.AddDays(3)
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var act = () => service.ReschedulePublishAsync(1, DateTime.UtcNow.AddDays(-1), "editor-1");
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*future*");
    }

    [Fact]
    public async Task PublishDueScheduledAsync_PublishesDueApprovedAsset()
    {
        using var db = CreateSqliteContext();
        db.AppUsers.Add(new AppUser
        {
            CognitoSub = "creator-1",
            Email = "creator@test.com",
            DisplayName = "Creator",
            Role = "ContentCreator"
        });
        db.MediaAssets.Add(new MediaAsset
        {
            Id = 1,
            Title = "Due Asset",
            CreatorId = "creator-1",
            Status = AssetStatus.Approved,
            S3Key = "k1",
            ContentType = "image/jpeg",
            ScheduledPublishAt = DateTime.UtcNow.AddMinutes(-1)
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var count = await service.PublishDueScheduledAsync();

        count.Should().Be(1);

        db.ChangeTracker.Clear();
        var asset = await db.MediaAssets.FindAsync(1);
        asset!.Status.Should().Be(AssetStatus.Published);
        asset.PublishedAt.Should().NotBeNull();
        asset.ScheduledPublishAt.Should().BeNull();
    }

    [Fact]
    public async Task PublishDueScheduledAsync_NotifiesCreatorOfPublishedAsset()
    {
        using var db = CreateSqliteContext();
        db.AppUsers.Add(new AppUser
        {
            CognitoSub = "creator-1",
            Email = "creator@test.com",
            DisplayName = "Creator",
            Role = "ContentCreator"
        });
        db.MediaAssets.Add(new MediaAsset
        {
            Id = 1,
            Title = "Scheduled Asset",
            CreatorId = "creator-1",
            Status = AssetStatus.Approved,
            S3Key = "k1",
            ContentType = "image/jpeg",
            ScheduledPublishAt = DateTime.UtcNow.AddMinutes(-1)
        });
        await db.SaveChangesAsync();

        var hubMock = new Mock<IHubContext<NotificationHub, INotificationClient>>();
        var clientMock = new Mock<INotificationClient>();
        hubMock.Setup(h => h.Clients.Group(It.IsAny<string>())).Returns(clientMock.Object);
        hubMock.Setup(h => h.Clients.Groups(It.IsAny<IReadOnlyList<string>>())).Returns(clientMock.Object);
        var notifMock = new Mock<INotificationService>();
        var auditMock = new Mock<IAuditLogService>();
        var s3Mock = new Mock<IS3StorageService>();

        var service = new ReviewService(db, hubMock.Object, notifMock.Object, auditMock.Object, s3Mock.Object, new Mock<IReviewEventPublisher>().Object);
        await service.PublishDueScheduledAsync();

        notifMock.Verify(n => n.CreateNotificationAsync(
            "creator-1", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationType>()),
            Times.Once);
        clientMock.Verify(c => c.ReceiveToast(
            It.IsAny<string>(),
            It.Is<string>(m => m.Contains("Scheduled Asset")),
            "success"), Times.Once);
    }

    [Fact]
    public async Task PublishDueScheduledAsync_WritesAuditLogEntry()
    {
        using var db = CreateSqliteContext();
        db.AppUsers.Add(new AppUser
        {
            CognitoSub = "creator-1",
            Email = "creator@test.com",
            DisplayName = "Creator",
            Role = "ContentCreator"
        });
        db.MediaAssets.Add(new MediaAsset
        {
            Id = 7,
            Title = "Audited Asset",
            CreatorId = "creator-1",
            Status = AssetStatus.Approved,
            S3Key = "k1",
            ContentType = "image/jpeg",
            ScheduledPublishAt = DateTime.UtcNow.AddMinutes(-1)
        });
        await db.SaveChangesAsync();

        var auditMock = new Mock<IAuditLogService>();
        var service = CreateService(db, auditMock: auditMock);
        await service.PublishDueScheduledAsync();

        auditMock.Verify(a => a.LogAsync(
            "Schedule.Published", "MediaAsset", "7", It.IsAny<object>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishDueScheduledAsync_DoesNotPublishAssetScheduledInTheFuture()
    {
        using var db = CreateSqliteContext();
        db.AppUsers.Add(new AppUser
        {
            CognitoSub = "creator-1",
            Email = "creator@test.com",
            DisplayName = "Creator",
            Role = "ContentCreator"
        });
        db.MediaAssets.Add(new MediaAsset
        {
            Id = 1,
            Title = "Future Asset",
            CreatorId = "creator-1",
            Status = AssetStatus.Approved,
            S3Key = "k1",
            ContentType = "image/jpeg",
            ScheduledPublishAt = DateTime.UtcNow.AddDays(1)
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var count = await service.PublishDueScheduledAsync();

        count.Should().Be(0);
        db.ChangeTracker.Clear();
        var asset = await db.MediaAssets.FindAsync(1);
        asset!.Status.Should().Be(AssetStatus.Approved);
    }

    [Fact]
    public async Task PublishDueScheduledAsync_DoesNotPublishUnscheduledApprovedAsset()
    {
        using var db = CreateSqliteContext();
        db.AppUsers.Add(new AppUser
        {
            CognitoSub = "creator-1",
            Email = "creator@test.com",
            DisplayName = "Creator",
            Role = "ContentCreator"
        });
        db.MediaAssets.Add(new MediaAsset
        {
            Id = 1,
            Title = "Unscheduled Asset",
            CreatorId = "creator-1",
            Status = AssetStatus.Approved,
            S3Key = "k1",
            ContentType = "image/jpeg",
            ScheduledPublishAt = null
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var count = await service.PublishDueScheduledAsync();

        count.Should().Be(0);
        db.ChangeTracker.Clear();
        var asset = await db.MediaAssets.FindAsync(1);
        asset!.Status.Should().Be(AssetStatus.Approved);
    }

    [Fact]
    public async Task PublishDueScheduledAsync_DoesNotPublishNonApprovedAsset()
    {
        using var db = CreateSqliteContext();
        db.AppUsers.Add(new AppUser
        {
            CognitoSub = "creator-1",
            Email = "creator@test.com",
            DisplayName = "Creator",
            Role = "ContentCreator"
        });
        db.MediaAssets.Add(new MediaAsset
        {
            Id = 1,
            Title = "Pending Asset",
            CreatorId = "creator-1",
            Status = AssetStatus.PendingReview,
            S3Key = "k1",
            ContentType = "image/jpeg",
            ScheduledPublishAt = DateTime.UtcNow.AddMinutes(-1)
        });
        await db.SaveChangesAsync();

        var notifMock = new Mock<INotificationService>();
        var service = CreateService(db, notifMock: notifMock);
        var count = await service.PublishDueScheduledAsync();

        count.Should().Be(0);
        db.ChangeTracker.Clear();
        var asset = await db.MediaAssets.FindAsync(1);
        asset!.Status.Should().Be(AssetStatus.PendingReview);
        notifMock.Verify(n => n.CreateNotificationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationType>()),
            Times.Never);
    }

    [Fact]
    public async Task PublishDueScheduledAsync_PublishesMultipleDueAssets_AndReturnsCount()
    {
        using var db = CreateSqliteContext();
        db.AppUsers.Add(new AppUser
        {
            CognitoSub = "creator-1",
            Email = "creator@test.com",
            DisplayName = "Creator",
            Role = "ContentCreator"
        });
        db.MediaAssets.AddRange(
            new MediaAsset
            {
                Id = 1,
                Title = "Due One",
                CreatorId = "creator-1",
                Status = AssetStatus.Approved,
                S3Key = "k1",
                ContentType = "image/jpeg",
                ScheduledPublishAt = DateTime.UtcNow.AddMinutes(-5)
            },
            new MediaAsset
            {
                Id = 2,
                Title = "Due Two",
                CreatorId = "creator-1",
                Status = AssetStatus.Approved,
                S3Key = "k2",
                ContentType = "image/png",
                ScheduledPublishAt = DateTime.UtcNow.AddMinutes(-1)
            });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var count = await service.PublishDueScheduledAsync();

        count.Should().Be(2);
        db.ChangeTracker.Clear();
        var assets = await db.MediaAssets.ToListAsync();
        assets.Should().OnlyContain(a => a.Status == AssetStatus.Published);
    }

    [Fact]
    public async Task SubmitDecisionAsync_EmitsReviewEventForTheEmailPipeline()
    {
        using var db = CreateInMemoryContext();
        db.AppUsers.Add(new AppUser
        {
            CognitoSub = "creator-1",
            Email = "creator@test.com",
            DisplayName = "Creator",
            Role = "ContentCreator"
        });
        db.MediaAssets.Add(new MediaAsset
        {
            Id = 1,
            Title = "Test Asset",
            CreatorId = "creator-1",
            Status = AssetStatus.PendingReview,
            S3Key = "key",
            ContentType = "image/jpeg"
        });
        await db.SaveChangesAsync();

        var eventPublisher = new Mock<IReviewEventPublisher>();
        var service = CreateService(db, eventPublisherMock: eventPublisher);
        await service.SubmitDecisionAsync(1, ReviewDecision.Approved, "reviewer-1", "Looks good");

        eventPublisher.Verify(p => p.PublishCreatorNotificationAsync(
            "creator-1",
            It.Is<string>(t => t.Contains("Approved")),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task BatchApproveAndPublishAsync_PublishesAlreadyApprovedAsset_WithoutAddingReview()
    {
        using var db = CreateInMemoryContext();
        db.AppUsers.Add(new AppUser
        {
            CognitoSub = "creator-1",
            Email = "creator@test.com",
            DisplayName = "Creator",
            Role = "ContentCreator"
        });
        db.MediaAssets.Add(new MediaAsset
        {
            Id = 1,
            Title = "Already Approved",
            CreatorId = "creator-1",
            Status = AssetStatus.Approved,
            ScheduledPublishAt = DateTime.UtcNow.AddDays(3),
            S3Key = "k1",
            ContentType = "image/jpeg"
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var processed = await service.BatchApproveAndPublishAsync(new[] { 1 }, "reviewer-1", null);

        processed.Should().Be(1);
        var asset = await db.MediaAssets.FindAsync(1);
        asset!.Status.Should().Be(AssetStatus.Published);
        asset.PublishedAt.Should().NotBeNull();
        asset.ScheduledPublishAt.Should().BeNull();
        (await db.Reviews.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task BatchApproveAndPublishAsync_AcceptsMixedPendingAndApproved_AddsReviewRowOnlyForUnapproved()
    {
        using var db = CreateInMemoryContext();
        db.AppUsers.Add(new AppUser
        {
            CognitoSub = "creator-1",
            Email = "creator@test.com",
            DisplayName = "Creator",
            Role = "ContentCreator"
        });
        db.MediaAssets.AddRange(
            new MediaAsset
            {
                Id = 1,
                Title = "Pending",
                CreatorId = "creator-1",
                Status = AssetStatus.PendingReview,
                S3Key = "k1",
                ContentType = "image/jpeg"
            },
            new MediaAsset
            {
                Id = 2,
                Title = "Already Approved",
                CreatorId = "creator-1",
                Status = AssetStatus.Approved,
                S3Key = "k2",
                ContentType = "image/png"
            });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var processed = await service.BatchApproveAndPublishAsync(new[] { 1, 2 }, "reviewer-1", "ok");

        processed.Should().Be(2);
        var assets = await db.MediaAssets.OrderBy(a => a.Id).ToListAsync();
        assets.Should().OnlyContain(a => a.Status == AssetStatus.Published);

        var reviews = await db.Reviews.ToListAsync();
        reviews.Should().ContainSingle();
        reviews[0].AssetId.Should().Be(1);
        reviews[0].Decision.Should().Be(ReviewDecision.Approved);
    }

    [Fact]
    public async Task BatchApproveAndPublishAsync_ReturnsProcessedCount_NotSelectedCount()
    {
        using var db = CreateInMemoryContext();
        db.AppUsers.Add(new AppUser
        {
            CognitoSub = "creator-1",
            Email = "creator@test.com",
            DisplayName = "Creator",
            Role = "ContentCreator"
        });
        db.MediaAssets.AddRange(
            new MediaAsset
            {
                Id = 1,
                Title = "Pending",
                CreatorId = "creator-1",
                Status = AssetStatus.PendingReview,
                S3Key = "k1",
                ContentType = "image/jpeg"
            },
            new MediaAsset
            {
                Id = 2,
                Title = "Draft (ineligible)",
                CreatorId = "creator-1",
                Status = AssetStatus.Draft,
                S3Key = "k2",
                ContentType = "image/jpeg"
            });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var processed = await service.BatchApproveAndPublishAsync(new[] { 1, 2 }, "reviewer-1", null);

        processed.Should().Be(1);
        var draft = await db.MediaAssets.FindAsync(2);
        draft!.Status.Should().Be(AssetStatus.Draft);
    }
}
