using FluentAssertions;
using MediaFlows.Shared.Models.Enums;

namespace MediaFlows.Services.Tests.Models;

public class EnumTests
{
    [Fact]
    public void AssetStatus_ShouldHaveExpectedValues()
    {
        Enum.GetNames<AssetStatus>().Should().HaveCount(10);
        Enum.IsDefined(AssetStatus.Draft).Should().BeTrue();
        Enum.IsDefined(AssetStatus.Submitted).Should().BeTrue();
        Enum.IsDefined(AssetStatus.PendingReview).Should().BeTrue();
        Enum.IsDefined(AssetStatus.Approved).Should().BeTrue();
        Enum.IsDefined(AssetStatus.Published).Should().BeTrue();
        Enum.IsDefined(AssetStatus.Rejected).Should().BeTrue();
        Enum.IsDefined(AssetStatus.ChangesRequested).Should().BeTrue();
        Enum.IsDefined(AssetStatus.Archived).Should().BeTrue();
        Enum.IsDefined(AssetStatus.Quarantined).Should().BeTrue();
        Enum.IsDefined(AssetStatus.Deleted).Should().BeTrue();
    }

    [Fact]
    public void ReviewDecision_ShouldHaveExpectedValues()
    {
        Enum.GetNames<ReviewDecision>().Should().HaveCount(3);
        Enum.IsDefined(ReviewDecision.Approved).Should().BeTrue();
        Enum.IsDefined(ReviewDecision.Rejected).Should().BeTrue();
        Enum.IsDefined(ReviewDecision.ChangesRequested).Should().BeTrue();
    }

    [Fact]
    public void MediaType_ShouldHaveExpectedValues()
    {
        Enum.GetNames<MediaType>().Should().HaveCount(5);
        Enum.IsDefined(MediaType.Image).Should().BeTrue();
        Enum.IsDefined(MediaType.Video).Should().BeTrue();
        Enum.IsDefined(MediaType.Audio).Should().BeTrue();
        Enum.IsDefined(MediaType.Document).Should().BeTrue();
        Enum.IsDefined(MediaType.Other).Should().BeTrue();
    }

    [Fact]
    public void NotificationType_ShouldHaveExpectedValues()
    {
        Enum.GetNames<NotificationType>().Should().HaveCount(6);
        Enum.IsDefined(NotificationType.ReviewDecision).Should().BeTrue();
        Enum.IsDefined(NotificationType.NewComment).Should().BeTrue();
        Enum.IsDefined(NotificationType.UploadComplete).Should().BeTrue();
        Enum.IsDefined(NotificationType.ModerationAlert).Should().BeTrue();
        Enum.IsDefined(NotificationType.SystemAnnouncement).Should().BeTrue();
        Enum.IsDefined(NotificationType.AssetShared).Should().BeTrue();
    }
}
