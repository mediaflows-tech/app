using FluentAssertions;
using MediaFlows.Shared.Models.Entities;
using MediaFlows.Shared.Models.Enums;
using MediaFlows.Shared.Models.ValueObjects;

namespace MediaFlows.Services.Tests.Models;

public class EntityDefaultTests
{
    [Fact]
    public void MediaAsset_DefaultStatus_ShouldBeDraft()
    {
        var asset = new MediaAsset();
        asset.Status.Should().Be(AssetStatus.Draft);
    }

    [Fact]
    public void MediaAsset_DefaultMetadata_ShouldNotBeNull()
    {
        var asset = new MediaAsset();
        asset.Metadata.Should().NotBeNull();
        asset.Metadata.AutoTags.Should().NotBeNull().And.BeEmpty();
        asset.Metadata.ExifTags.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void MediaAsset_Collections_ShouldBeInitialized()
    {
        var asset = new MediaAsset();
        asset.Versions.Should().NotBeNull().And.BeEmpty();
        asset.Reviews.Should().NotBeNull().And.BeEmpty();
        asset.Comments.Should().NotBeNull().And.BeEmpty();
        asset.Bookmarks.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void AppUser_DefaultRole_ShouldBeViewer()
    {
        var user = new AppUser();
        user.Role.Should().Be("Viewer");
    }

    [Fact]
    public void AppUser_DefaultIsActive_ShouldBeTrue()
    {
        var user = new AppUser();
        user.IsActive.Should().BeTrue();
    }

    [Fact]
    public void MediaMetadata_DefaultCollections_ShouldBeEmpty()
    {
        var metadata = new MediaMetadata();
        metadata.AutoTags.Should().NotBeNull().And.BeEmpty();
        metadata.ExifTags.Should().NotBeNull().And.BeEmpty();
        metadata.Moderation.Should().BeNull();
    }

    [Fact]
    public void Project_DefaultIsActive_ShouldBeTrue()
    {
        var project = new Project();
        project.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Notification_DefaultIsRead_ShouldBeFalse()
    {
        var notification = new Notification();
        notification.IsRead.Should().BeFalse();
    }

    [Fact]
    public void MediaAsset_DefaultIsDeleted_ShouldBeFalse()
    {
        var asset = new MediaAsset();
        asset.IsDeleted.Should().BeFalse();
    }
}
