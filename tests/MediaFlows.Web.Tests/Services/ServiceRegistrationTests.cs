using FluentAssertions;
using MediaFlows.Shared.Interfaces;
using MediaFlows.Web.Services;

namespace MediaFlows.Web.Tests.Services;

public class ServiceRegistrationTests
{
    [Fact]
    public void MediaAssetService_ImplementsIMediaAssetService()
    {
        typeof(MediaAssetService).Should().Implement<IMediaAssetService>();
    }

    [Fact]
    public void UploadService_ImplementsIUploadService()
    {
        typeof(UploadService).Should().Implement<IUploadService>();
    }

    [Fact]
    public void ReviewService_ImplementsIReviewService()
    {
        typeof(ReviewService).Should().Implement<IReviewService>();
    }

    [Fact]
    public void SearchService_ImplementsISearchService()
    {
        typeof(SearchService).Should().Implement<ISearchService>();
    }

    [Fact]
    public void NotificationService_ImplementsINotificationService()
    {
        typeof(NotificationService).Should().Implement<INotificationService>();
    }

    [Fact]
    public void BookmarkService_ImplementsIBookmarkService()
    {
        typeof(BookmarkService).Should().Implement<IBookmarkService>();
    }

    [Fact]
    public void CommentService_ImplementsICommentService()
    {
        typeof(CommentService).Should().Implement<ICommentService>();
    }

    [Fact]
    public void AuditLogService_ImplementsIAuditLogService()
    {
        typeof(AuditLogService).Should().Implement<IAuditLogService>();
    }

    [Fact]
    public void AnalyticsService_ImplementsIAnalyticsService()
    {
        typeof(AnalyticsService).Should().Implement<IAnalyticsService>();
    }

    [Fact]
    public void S3StorageService_ImplementsIS3StorageService()
    {
        typeof(S3StorageService).Should().Implement<IS3StorageService>();
    }

    [Fact]
    public void DynamoDbService_ImplementsIDynamoDbService()
    {
        typeof(DynamoDbService).Should().Implement<IDynamoDbService>();
    }

    [Fact]
    public void CognitoAdminService_ImplementsICognitoAdminService()
    {
        typeof(CognitoAdminService).Should().Implement<ICognitoAdminService>();
    }

}
