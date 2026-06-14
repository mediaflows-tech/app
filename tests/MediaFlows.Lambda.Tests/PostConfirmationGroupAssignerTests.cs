using System.Text.Json;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.Lambda.TestUtilities;
using FluentAssertions;
using MediaFlows.Lambda.PostConfirmationGroupAssigner;
using Moq;

namespace MediaFlows.Lambda.Tests;

public class PostConfirmationGroupAssignerTests
{
    private readonly Mock<IAmazonCognitoIdentityProvider> _cognito = new();
    private readonly Function _sut;
    private readonly TestLambdaContext _ctx = new();

    public PostConfirmationGroupAssignerTests()
    {
        _sut = new Function(_cognito.Object);
    }

    private static CognitoPostConfirmationEvent SignUpEvent() => new()
    {
        TriggerSource = "PostConfirmation_ConfirmSignUp",
        UserPoolId = "pool",
        UserName = "alice"
    };

    private static JsonElement? FindEmfLine(string logBuffer)
    {
        foreach (var line in logBuffer.Split('\n'))
        {
            if (!line.Contains("\"_aws\"")) continue;
            try
            {
                var doc = JsonDocument.Parse(line.Trim());
                if (doc.RootElement.TryGetProperty("_aws", out _))
                {
                    return doc.RootElement;
                }
            }
            catch (JsonException)
            {
                // Not JSON or partial line — skip.
            }
        }
        return null;
    }

    [Fact]
    public async Task FunctionHandler_EmitsEmfMetricLineOnAdminAddUserToGroupFailure()
    {
        _cognito
            .Setup(c => c.AdminAddUserToGroupAsync(
                It.IsAny<AdminAddUserToGroupRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("simulated cognito outage"));

        var input = SignUpEvent();

        var result = await _sut.FunctionHandler(input, _ctx);

        // Contract preservation: handler must return the event unchanged so
        // Cognito's confirmation flow proceeds.
        result.Should().BeSameAs(input);

        var buf = ((TestLambdaLogger)_ctx.Logger).Buffer.ToString();
        var emf = FindEmfLine(buf);
        emf.Should().NotBeNull("an EMF metric line should be emitted on failure");

        var cw = emf!.Value.GetProperty("_aws").GetProperty("CloudWatchMetrics")[0];
        cw.GetProperty("Namespace").GetString().Should().Be("MediaFlows/PostConfirmation");
        cw.GetProperty("Metrics")[0].GetProperty("Name").GetString().Should().Be("GroupAssignFailures");
        emf.Value.GetProperty("GroupAssignFailures").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task FunctionHandler_EmfLineCarriesGroupNameDimension()
    {
        _cognito
            .Setup(c => c.AdminAddUserToGroupAsync(
                It.IsAny<AdminAddUserToGroupRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("simulated cognito outage"));

        await _sut.FunctionHandler(SignUpEvent(), _ctx);

        var emf = FindEmfLine(((TestLambdaLogger)_ctx.Logger).Buffer.ToString());
        emf.Should().NotBeNull();

        emf!.Value.GetProperty("GroupName").GetString().Should().Be("Viewer");

        var dimensionSets = emf.Value
            .GetProperty("_aws")
            .GetProperty("CloudWatchMetrics")[0]
            .GetProperty("Dimensions")
            .EnumerateArray()
            .Select(set => set.EnumerateArray().Select(d => d.GetString()).ToList())
            .ToList();
        dimensionSets.Should().Contain(set => set.Contains("GroupName"));
    }

    private class FakeCognitoFailure : Exception
    {
        public FakeCognitoFailure(string message) : base(message) { }
    }

    [Fact]
    public async Task FunctionHandler_EmfLineCarriesExceptionTypeDimension()
    {
        _cognito
            .Setup(c => c.AdminAddUserToGroupAsync(
                It.IsAny<AdminAddUserToGroupRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FakeCognitoFailure("simulated"));

        await _sut.FunctionHandler(SignUpEvent(), _ctx);

        var emf = FindEmfLine(((TestLambdaLogger)_ctx.Logger).Buffer.ToString());
        emf.Should().NotBeNull();

        emf!.Value.GetProperty("ExceptionType").GetString().Should().Be("FakeCognitoFailure");

        var dimensionSets = emf.Value
            .GetProperty("_aws")
            .GetProperty("CloudWatchMetrics")[0]
            .GetProperty("Dimensions")
            .EnumerateArray()
            .Select(set => set.EnumerateArray().Select(d => d.GetString()).ToList())
            .ToList();
        dimensionSets.Should().Contain(set => set.Contains("ExceptionType"));
    }

    [Fact]
    public async Task FunctionHandler_HappyPathEmitsNoEmfMetricLine()
    {
        _cognito
            .Setup(c => c.AdminAddUserToGroupAsync(
                It.IsAny<AdminAddUserToGroupRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminAddUserToGroupResponse());

        await _sut.FunctionHandler(SignUpEvent(), _ctx);

        var emf = FindEmfLine(((TestLambdaLogger)_ctx.Logger).Buffer.ToString());
        emf.Should().BeNull("happy path must not raise the failure metric — alarm baseline must stay at zero");
    }
}
