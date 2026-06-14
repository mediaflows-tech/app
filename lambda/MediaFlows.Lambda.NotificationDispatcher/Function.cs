using Amazon.Lambda.Core;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.XRay.Recorder.Handlers.AwsSdk;
using System.Text.Json;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace MediaFlows.Lambda.NotificationDispatcher;

public class EventBridgeEvent
{
    public string DetailType { get; set; } = "";
    public JsonElement Detail { get; set; }
    public string Source { get; set; } = "";
    public DateTime Time { get; set; }
    public string Id { get; set; } = "";
    public string Region { get; set; } = "";
    public List<string> Resources { get; set; } = new();
}

public class Function
{
    private readonly IAmazonSimpleNotificationService _sns;
    private readonly string _topicArn;

    static Function()
    {
        AWSSDKHandler.RegisterXRayForAllServices();
    }

    public Function()
    {
        _topicArn = Environment.GetEnvironmentVariable("NOTIFICATION_TOPIC_ARN")
            ?? throw new InvalidOperationException("NOTIFICATION_TOPIC_ARN environment variable is not set");
        _sns = new AmazonSimpleNotificationServiceClient();
    }

    public Function(IAmazonSimpleNotificationService sns, string topicArn)
    {
        _sns = sns;
        _topicArn = topicArn;
    }

    public async Task FunctionHandler(EventBridgeEvent input, ILambdaContext context)
    {
        context.Logger.LogInformation($"Processing EventBridge event: {input.Id}, type: {input.DetailType}");

        try
        {
            var messageBody = JsonSerializer.Serialize(input.Detail, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            var publishRequest = new PublishRequest
            {
                TopicArn = _topicArn,
                Subject = input.DetailType,
                Message = messageBody,
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    ["EventType"] = new MessageAttributeValue
                    {
                        DataType = "String",
                        StringValue = input.DetailType
                    },
                    ["Source"] = new MessageAttributeValue
                    {
                        DataType = "String",
                        StringValue = input.Source
                    },
                    ["Timestamp"] = new MessageAttributeValue
                    {
                        DataType = "String",
                        StringValue = input.Time.ToString("O")
                    }
                }
            };

            var response = await _sns.PublishAsync(publishRequest);

            context.Logger.LogInformation(
                $"Notification dispatched successfully. MessageId: {response.MessageId}, " +
                $"DetailType: {input.DetailType}, Source: {input.Source}");
        }
        catch (Exception ex)
        {
            context.Logger.LogError(
                $"Failed to dispatch notification for event {input.Id}: {ex.Message}");
            throw;
        }
    }
}
