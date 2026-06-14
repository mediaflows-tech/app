using Amazon.Lambda.TestUtilities;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using FluentAssertions;
using MediaFlows.Lambda.NotificationDispatcher;
using Moq;
using System.Text.Json;

namespace MediaFlows.Lambda.Tests;

public class NotificationDispatcherTests
{
    private const string TopicArn = "arn:aws:sns:ap-southeast-1:123456789:notification-topic";
    private readonly Mock<IAmazonSimpleNotificationService> _sns;
    private readonly Function _sut;
    private readonly TestLambdaContext _context;

    public NotificationDispatcherTests()
    {
        _sns = new Mock<IAmazonSimpleNotificationService>();
        _sns.Setup(x => x.PublishAsync(It.IsAny<PublishRequest>(), default))
            .ReturnsAsync(new PublishResponse { MessageId = "test-message-id" });
        _sut = new Function(_sns.Object, TopicArn);
        _context = new TestLambdaContext();
    }

    private static EventBridgeEvent CreateEvent(string detailType, object detail, string source = "mediaflows.reviews")
    {
        var detailJson = JsonSerializer.Serialize(detail);
        var detailElement = JsonSerializer.Deserialize<JsonElement>(detailJson);

        return new EventBridgeEvent
        {
            DetailType = detailType,
            Detail = detailElement,
            Source = source,
            Time = new DateTime(2026, 3, 19, 12, 0, 0, DateTimeKind.Utc),
            Id = "evt-001",
            Region = "ap-southeast-1",
            Resources = new List<string>()
        };
    }

    [Fact]
    public async Task FunctionHandler_PublishesToSns_WithCorrectAttributes()
    {
        var input = CreateEvent("ReviewApproved", new { AssetId = "asset-123", ReviewerId = "user-456" });

        await _sut.FunctionHandler(input, _context);

        _sns.Verify(x => x.PublishAsync(It.Is<PublishRequest>(r =>
            r.TopicArn == TopicArn &&
            r.Subject == "ReviewApproved" &&
            r.MessageAttributes["EventType"].StringValue == "ReviewApproved" &&
            r.MessageAttributes["EventType"].DataType == "String" &&
            r.MessageAttributes["Source"].StringValue == "mediaflows.reviews" &&
            r.MessageAttributes["Source"].DataType == "String" &&
            r.MessageAttributes["Timestamp"].StringValue == "2026-03-19T12:00:00.0000000Z" &&
            r.MessageAttributes["Timestamp"].DataType == "String"
        ), default), Times.Once);
    }

    [Fact]
    public async Task FunctionHandler_SnsFailure_RethrowsException()
    {
        _sns.Setup(x => x.PublishAsync(It.IsAny<PublishRequest>(), default))
            .ThrowsAsync(new AmazonSimpleNotificationServiceException("SNS unavailable"));

        var input = CreateEvent("ReviewRejected", new { AssetId = "asset-789" });

        var act = () => _sut.FunctionHandler(input, _context);

        await act.Should().ThrowAsync<AmazonSimpleNotificationServiceException>()
            .WithMessage("SNS unavailable");
    }

    [Fact]
    public void FunctionHandler_MissingTopicArn_ThrowsInvalidOperationException()
    {
        var original = Environment.GetEnvironmentVariable("NOTIFICATION_TOPIC_ARN");
        try
        {
            Environment.SetEnvironmentVariable("NOTIFICATION_TOPIC_ARN", null);

            var act = () => new Function();

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*NOTIFICATION_TOPIC_ARN*");
        }
        finally
        {
            Environment.SetEnvironmentVariable("NOTIFICATION_TOPIC_ARN", original);
        }
    }

    [Fact]
    public async Task FunctionHandler_SerializesDetailToMessageBody()
    {
        PublishRequest? capturedRequest = null;
        _sns.Setup(x => x.PublishAsync(It.IsAny<PublishRequest>(), default))
            .Callback<PublishRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new PublishResponse { MessageId = "msg-123" });

        var detail = new { AssetId = "asset-abc", Status = "approved", Comment = "Looks great" };
        var input = CreateEvent("ReviewCompleted", detail);

        await _sut.FunctionHandler(input, _context);

        capturedRequest.Should().NotBeNull();
        var deserialized = JsonSerializer.Deserialize<JsonElement>(capturedRequest!.Message);
        deserialized.GetProperty("AssetId").GetString().Should().Be("asset-abc");
        deserialized.GetProperty("Status").GetString().Should().Be("approved");
        deserialized.GetProperty("Comment").GetString().Should().Be("Looks great");
    }
}
