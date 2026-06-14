using FluentAssertions;
using MediaFlows.Data;
using MediaFlows.Shared.Models.Entities;
using MediaFlows.Shared.Models.Enums;
using MediaFlows.Tests.Common;
using MediaFlows.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Xunit;

namespace MediaFlows.Services.Tests;

public class NotificationServiceTests
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
    public async Task CreateNotificationAsync_PersistsNotification()
    {
        using var db = CreateInMemoryContext();
        var service = new NotificationService(db);

        await service.CreateNotificationAsync("user-1", "Test", "Message", NotificationType.ReviewDecision);

        var notification = await db.Notifications.FirstAsync();
        notification.UserId.Should().Be("user-1");
        notification.Title.Should().Be("Test");
        notification.IsRead.Should().BeFalse();
    }

    [Fact]
    public async Task GetUnreadCountAsync_ReturnsCorrectCount()
    {
        using var db = CreateInMemoryContext();
        db.Notifications.AddRange(
            new Notification
            {
                UserId = "user-1",
                Title = "A",
                Message = "M",
                Type = NotificationType.ReviewDecision,
                IsRead = false
            },
            new Notification
            {
                UserId = "user-1",
                Title = "B",
                Message = "M",
                Type = NotificationType.ReviewDecision,
                IsRead = true
            },
            new Notification
            {
                UserId = "user-1",
                Title = "C",
                Message = "M",
                Type = NotificationType.ReviewDecision,
                IsRead = false
            }
        );
        await db.SaveChangesAsync();

        var service = new NotificationService(db);
        var count = await service.GetUnreadCountAsync("user-1");

        count.Should().Be(2);
    }

    [Fact]
    public async Task MarkAllReadAsync_UpdatesAllUnreadNotifications()
    {
        using var db = CreateInMemoryContext();
        db.Notifications.AddRange(
            new Notification
            {
                UserId = "user-1",
                Title = "A",
                Message = "M",
                Type = NotificationType.ReviewDecision,
                IsRead = false
            },
            new Notification
            {
                UserId = "user-1",
                Title = "B",
                Message = "M",
                Type = NotificationType.ReviewDecision,
                IsRead = false
            }
        );
        await db.SaveChangesAsync();

        var service = new NotificationService(db);
        await service.MarkAllReadAsync("user-1");

        var unread = await db.Notifications.CountAsync(n => !n.IsRead && n.UserId == "user-1");
        unread.Should().Be(0);
    }

    [Fact]
    public async Task GetUserNotificationsAsync_ReturnsOrderedByDate()
    {
        using var db = CreateInMemoryContext();
        var oldNotif = new Notification
        {
            UserId = "u1",
            Title = "Old",
            Message = "m1",
            Type = NotificationType.SystemAnnouncement
        };
        var newNotif = new Notification
        {
            UserId = "u1",
            Title = "New",
            Message = "m2",
            Type = NotificationType.SystemAnnouncement
        };
        var otherNotif = new Notification
        {
            UserId = "u2",
            Title = "Other",
            Message = "m3",
            Type = NotificationType.SystemAnnouncement
        };
        db.Notifications.AddRange(oldNotif, newNotif, otherNotif);
        await db.SaveChangesAsync();

        // Overwrite CreatedAt after save (SaveChangesAsync sets CreatedAt = UtcNow for Added entities)
        var baseDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        oldNotif.CreatedAt = baseDate;
        newNotif.CreatedAt = baseDate.AddHours(2);
        otherNotif.CreatedAt = baseDate.AddHours(3);
        await db.SaveChangesAsync();

        var service = new NotificationService(db);
        var result = await service.GetUserNotificationsAsync("u1");

        result.Should().HaveCount(2);
        result.First().Title.Should().Be("New");
    }

    [Fact]
    public async Task GetUserNotificationsAsync_RespectsLimit()
    {
        using var db = CreateInMemoryContext();
        for (int i = 0; i < 5; i++)
        {
            db.Notifications.Add(new Notification
            {
                UserId = "u1",
                Title = $"Notif {i}",
                Message = "msg",
                Type = NotificationType.SystemAnnouncement,
                CreatedAt = DateTime.UtcNow.AddMinutes(-i)
            });
        }
        await db.SaveChangesAsync();

        var service = new NotificationService(db);
        var result = await service.GetUserNotificationsAsync("u1", limit: 3);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task MarkReadAsync_SetsIsReadTrue()
    {
        using var db = CreateInMemoryContext();
        var notification = new Notification
        {
            UserId = "u1",
            Title = "Unread",
            Message = "msg",
            Type = NotificationType.SystemAnnouncement,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };
        db.Notifications.Add(notification);
        await db.SaveChangesAsync();

        var service = new NotificationService(db);
        await service.MarkReadAsync(notification.Id);

        var updated = await db.Notifications.FindAsync(notification.Id);
        updated!.IsRead.Should().BeTrue();
    }

    [Fact]
    public async Task MarkReadAsync_DoesNothing_WhenNotificationNotFound()
    {
        using var db = CreateInMemoryContext();
        var service = new NotificationService(db);

        // Should not throw
        await service.MarkReadAsync(999);
    }
}
