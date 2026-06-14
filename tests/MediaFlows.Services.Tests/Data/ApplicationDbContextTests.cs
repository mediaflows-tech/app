using FluentAssertions;
using MediaFlows.Data;
using MediaFlows.Shared.Models.Entities;
using MediaFlows.Shared.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace MediaFlows.Services.Tests.Data;

/// <summary>
/// Test subclass that ignores Npgsql-specific features (TsVector, JSONB)
/// which the InMemory provider does not support.
/// </summary>
public class TestApplicationDbContext : ApplicationDbContext
{
    public TestApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Skip base to avoid Npgsql-specific configurations (TsVector, JSONB ToJson)
        // Apply only relationship configurations needed for InMemory testing
        modelBuilder.Entity<AppUser>().HasKey(u => u.CognitoSub);
        modelBuilder.Entity<MediaAsset>().Ignore(a => a.SearchVector);
        modelBuilder.Entity<MediaAsset>().Ignore(a => a.Metadata);
        modelBuilder.Entity<AuditLog>().Ignore(a => a.SearchVector);
        modelBuilder.Entity<MediaAsset>().HasQueryFilter(a => !a.IsDeleted);

        modelBuilder.Entity<AssetVersion>()
            .HasOne(v => v.Asset).WithMany(a => a.Versions).HasForeignKey(v => v.AssetId);
        modelBuilder.Entity<AssetVersion>()
            .HasOne(v => v.UploadedBy).WithMany().HasForeignKey(v => v.UploadedById);
        modelBuilder.Entity<Review>()
            .HasOne(r => r.Asset).WithMany(a => a.Reviews).HasForeignKey(r => r.AssetId);
        modelBuilder.Entity<Review>()
            .HasOne(r => r.Reviewer).WithMany().HasForeignKey(r => r.ReviewerId);
        modelBuilder.Entity<Comment>()
            .HasOne(c => c.Asset).WithMany(a => a.Comments).HasForeignKey(c => c.AssetId);
        modelBuilder.Entity<Comment>()
            .HasOne(c => c.Author).WithMany().HasForeignKey(c => c.AuthorId);
        modelBuilder.Entity<Comment>()
            .HasOne(c => c.ParentComment).WithMany(c => c.Replies).HasForeignKey(c => c.ParentCommentId);
        modelBuilder.Entity<Bookmark>()
            .HasOne(b => b.Asset).WithMany(a => a.Bookmarks).HasForeignKey(b => b.AssetId);
        modelBuilder.Entity<Bookmark>()
            .HasOne(b => b.User).WithMany().HasForeignKey(b => b.UserId);
        modelBuilder.Entity<Notification>()
            .HasOne(n => n.User).WithMany().HasForeignKey(n => n.UserId);
        modelBuilder.Entity<Project>()
            .HasOne(p => p.Owner).WithMany().HasForeignKey(p => p.OwnerId);
        modelBuilder.Entity<MediaAsset>()
            .HasOne(a => a.Creator).WithMany().HasForeignKey(a => a.CreatorId);
        modelBuilder.Entity<MediaAsset>()
            .HasOne(a => a.Project).WithMany(p => p.Assets).HasForeignKey(a => a.ProjectId);
    }
}

public class ApplicationDbContextTests : IDisposable
{
    private readonly ApplicationDbContext _context;

    public ApplicationDbContextTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new TestApplicationDbContext(options);
    }

    [Fact]
    public async Task SaveChangesAsync_SetsCreatedAtOnNewEntity()
    {
        var user = new AppUser
        {
            CognitoSub = "test-sub-1",
            Email = "test@example.com",
            DisplayName = "Test User",
            Role = "Viewer"
        };

        _context.AppUsers.Add(user);
        await _context.SaveChangesAsync();

        user.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        user.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task SaveChangesAsync_UpdatesUpdatedAtOnModifiedEntity()
    {
        var user = new AppUser
        {
            CognitoSub = "test-sub-2",
            Email = "test2@example.com",
            DisplayName = "Test User 2",
            Role = "Viewer"
        };

        _context.AppUsers.Add(user);
        await _context.SaveChangesAsync();
        var originalCreatedAt = user.CreatedAt;

        await Task.Delay(10);

        user.DisplayName = "Updated Name";
        await _context.SaveChangesAsync();

        user.CreatedAt.Should().Be(originalCreatedAt);
        user.UpdatedAt.Should().BeAfter(originalCreatedAt);
    }

    [Fact]
    public async Task AppUsers_CanAddAndRetrieve()
    {
        var user = new AppUser
        {
            CognitoSub = "test-sub-3",
            Email = "retrieve@example.com",
            DisplayName = "Retrieve Test",
            Role = "SystemAdmin"
        };

        _context.AppUsers.Add(user);
        await _context.SaveChangesAsync();

        var found = await _context.AppUsers.FindAsync("test-sub-3");
        found.Should().NotBeNull();
        found!.Email.Should().Be("retrieve@example.com");
        found.Role.Should().Be("SystemAdmin");
    }

    [Fact]
    public async Task Notifications_DefaultIsReadIsFalse()
    {
        var user = new AppUser
        {
            CognitoSub = "notif-user",
            Email = "notif@example.com",
            DisplayName = "Notif User",
            Role = "Viewer"
        };
        _context.AppUsers.Add(user);
        await _context.SaveChangesAsync();

        var notification = new Notification
        {
            UserId = "notif-user",
            Type = NotificationType.SystemAnnouncement,
            Title = "Test",
            Message = "Test message"
        };
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        var found = await _context.Notifications.FirstAsync();
        found.IsRead.Should().BeFalse();
    }

    [Fact]
    public void DbSets_AreAllExposed()
    {
        _context.AppUsers.Should().NotBeNull();
        _context.Projects.Should().NotBeNull();
        _context.MediaAssets.Should().NotBeNull();
        _context.AssetVersions.Should().NotBeNull();
        _context.Reviews.Should().NotBeNull();
        _context.Notifications.Should().NotBeNull();
        _context.Comments.Should().NotBeNull();
        _context.Bookmarks.Should().NotBeNull();
        _context.Categories.Should().NotBeNull();
        _context.AuditLogs.Should().NotBeNull();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
