using System.Text.Json;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.Lambda.Core;
using Amazon.XRay.Recorder.Handlers.AwsSdk;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace MediaFlows.Lambda.PostConfirmationGroupAssigner;

public class CognitoPostConfirmationEvent
{
    public string Version { get; set; } = "";
    public string Region { get; set; } = "";
    public string UserPoolId { get; set; } = "";
    public string UserName { get; set; } = "";
    public CallerContext CallerContext { get; set; } = new();
    public string TriggerSource { get; set; } = "";
    public Request Request { get; set; } = new();
    public Dictionary<string, object> Response { get; set; } = new();
}

public class CallerContext
{
    public string AwsSdkVersion { get; set; } = "";
    public string ClientId { get; set; } = "";
}

public class Request
{
    public Dictionary<string, string> UserAttributes { get; set; } = new();
}

public class Function
{
    private readonly IAmazonCognitoIdentityProvider _cognito;
    private const string DefaultGroupName = "Viewer";
    private const string SignUpTriggerSource = "PostConfirmation_ConfirmSignUp";

    static Function()
    {
        AWSSDKHandler.RegisterXRayForAllServices();
    }

    public Function()
    {
        _cognito = new AmazonCognitoIdentityProviderClient();
    }

    public Function(IAmazonCognitoIdentityProvider cognito)
    {
        _cognito = cognito;
    }

    // Cognito post-confirmation trigger handler.
    //
    // CRITICAL: This handler must return the event unchanged (Cognito uses
    // the returned event to continue the flow). It must ALSO never throw —
    // any exception would make Cognito report the ConfirmSignUp API call as
    // failed, confusing the user even though their email is already confirmed.
    // We log errors and return the event regardless.
    public async Task<CognitoPostConfirmationEvent> FunctionHandler(
        CognitoPostConfirmationEvent input,
        ILambdaContext context)
    {
        context.Logger.LogInformation(
            $"Post-confirmation trigger fired. TriggerSource={input.TriggerSource}, " +
            $"UserPoolId={input.UserPoolId}, UserName={input.UserName}");

        // Only run on self-signup confirmations. Password-reset confirmations
        // reuse the same Lambda hook but the user already has their groups.
        if (input.TriggerSource != SignUpTriggerSource)
        {
            context.Logger.LogInformation(
                $"Skipping group assignment — trigger source '{input.TriggerSource}' " +
                $"is not '{SignUpTriggerSource}'.");
            return input;
        }

        try
        {
            await _cognito.AdminAddUserToGroupAsync(new AdminAddUserToGroupRequest
            {
                UserPoolId = input.UserPoolId,
                Username = input.UserName,
                GroupName = DefaultGroupName
            });

            context.Logger.LogInformation(
                $"Successfully added user '{input.UserName}' to group '{DefaultGroupName}'.");
        }
        catch (Exception ex)
        {
            // Log the error but do NOT throw — we want the confirmation to
            // succeed from the user's perspective. An admin can manually
            // add the user to the group later if needed.
            context.Logger.LogError(
                $"Failed to add user '{input.UserName}' to group '{DefaultGroupName}': " +
                $"{ex.GetType().Name}: {ex.Message}");

            EmitFailureMetric(context, ex);
        }

        return input;
    }

    private static void EmitFailureMetric(ILambdaContext context, Exception ex)
    {
        var emf = new
        {
            _aws = new
            {
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                CloudWatchMetrics = new[]
                {
                    new
                    {
                        Namespace = "MediaFlows/PostConfirmation",
                        // Empty dim set publishes a total-count stream the alarm
                        // can watch; the second set lets dashboards triage by
                        // group + exception.
                        Dimensions = new[] { Array.Empty<string>(), new[] { "GroupName", "ExceptionType" } },
                        Metrics = new[] { new { Name = "GroupAssignFailures", Unit = "Count" } }
                    }
                }
            },
            GroupName = DefaultGroupName,
            ExceptionType = ex.GetType().Name,
            GroupAssignFailures = 1
        };
        context.Logger.LogLine(JsonSerializer.Serialize(emf));
    }
}
