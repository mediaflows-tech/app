using FluentAssertions;
using MediaFlows.Data;
using MediaFlows.Shared.Models.Entities;
using MediaFlows.Shared.Models.Enums;
using MediaFlows.Tests.Common;
using MediaFlows.Web.Hubs;
using MediaFlows.Web.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Moq;
using Xunit;

namespace MediaFlows.Services.Tests;

public class CommentServiceTests
{
    private TestDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ReplaceService<IModelCacheKeyFactory, TestModelCacheKeyFactory>()
            .Options;
        return new TestDbContext(options);
    }

    private CommentService CreateService(ApplicationDbContext db)
    {
        var hubMock = new Mock<IHubContext<NotificationHub, INotificationClient>>();
        var clientMock = new Mock<INotificationClient>();
        hubMock.Setup(h => h.Clients.Group(It.IsAny<string>())).Returns(clientMock.Object);
        return new CommentService(db, hubMock.Object);
    }

    private async Task SeedTestData(ApplicationDbContext db)
    {
        db.AppUsers.Add(new AppUser
        {
            CognitoSub = "user-1",
            Email = "user@test.com",
            DisplayName = "Test User",
            Role = "Viewer"
        });
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
            S3Key = "key",
            ContentType = "image/jpeg",
            Status = AssetStatus.Approved
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task AddCommentAsync_CreatesComment_ReturnsDto()
    {
        using var db = CreateInMemoryContext();
        await SeedTestData(db);

        var service = CreateService(db);
        var result = await service.AddCommentAsync(1, "user-1", "Great photo!");

        result.Content.Should().Be("Great photo!");
        result.AssetId.Should().Be(1);
        result.AuthorDisplayName.Should().Be("Test User");
        result.ParentCommentId.Should().BeNull();

        (await db.Comments.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task AddCommentAsync_WithParent_CreatesReply()
    {
        using var db = CreateInMemoryContext();
        await SeedTestData(db);

        var service = CreateService(db);
        var parent = await service.AddCommentAsync(1, "user-1", "Parent comment");
        var reply = await service.AddCommentAsync(1, "creator-1", "Thanks!", parent.Id);

        reply.ParentCommentId.Should().Be(parent.Id);
        (await db.Comments.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task AddCommentAsync_InvalidParent_Throws()
    {
        using var db = CreateInMemoryContext();
        await SeedTestData(db);

        var service = CreateService(db);

        var act = () => service.AddCommentAsync(1, "user-1", "Reply", parentCommentId: 999);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Parent comment not found*");
    }

    [Fact]
    public async Task UpdateCommentAsync_OwnComment_UpdatesContent()
    {
        using var db = CreateInMemoryContext();
        await SeedTestData(db);

        var service = CreateService(db);
        var comment = await service.AddCommentAsync(1, "user-1", "Original");

        var updated = await service.UpdateCommentAsync(comment.Id, "user-1", "Updated content");

        updated.Should().NotBeNull();
        updated!.Content.Should().Be("Updated content");
    }

    [Fact]
    public async Task UpdateCommentAsync_OtherUsersComment_ThrowsUnauthorized()
    {
        using var db = CreateInMemoryContext();
        await SeedTestData(db);

        var service = CreateService(db);
        var comment = await service.AddCommentAsync(1, "user-1", "My comment");

        var act = () => service.UpdateCommentAsync(comment.Id, "other-user", "Hacked!");
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*only edit your own*");
    }

    [Fact]
    public async Task DeleteCommentAsync_SoftDeletes_ReplacesContent()
    {
        using var db = CreateInMemoryContext();
        await SeedTestData(db);

        var service = CreateService(db);
        var comment = await service.AddCommentAsync(1, "user-1", "To be deleted");

        var result = await service.DeleteCommentAsync(comment.Id, "user-1");

        result.Should().BeTrue();
        var deleted = await db.Comments.FindAsync(comment.Id);
        deleted!.Content.Should().Be("[deleted]");
    }

    [Fact]
    public async Task DeleteCommentAsync_OtherUsersComment_ThrowsUnauthorized()
    {
        using var db = CreateInMemoryContext();
        await SeedTestData(db);

        var service = CreateService(db);
        var comment = await service.AddCommentAsync(1, "user-1", "My comment");

        var act = () => service.DeleteCommentAsync(comment.Id, "other-user");
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetCommentsAsync_ReturnsTopLevelWithReplies()
    {
        using var db = CreateInMemoryContext();
        await SeedTestData(db);

        var service = CreateService(db);
        var parent = await service.AddCommentAsync(1, "user-1", "Top-level");
        await service.AddCommentAsync(1, "creator-1", "Reply", parent.Id);

        var comments = await service.GetCommentsAsync(1, "user-1");

        comments.Should().HaveCount(1);
        comments.First().Replies.Should().HaveCount(1);
        comments.First().IsOwner.Should().BeTrue();
        comments.First().Replies.First().IsOwner.Should().BeFalse();
    }

    [Fact]
    public async Task GetCommentCountAsync_ReturnsAllComments()
    {
        using var db = CreateInMemoryContext();
        await SeedTestData(db);

        var service = CreateService(db);
        var parent = await service.AddCommentAsync(1, "user-1", "Comment 1");
        await service.AddCommentAsync(1, "user-1", "Reply", parent.Id);

        var count = await service.GetCommentCountAsync(1);
        count.Should().Be(2);
    }
}
