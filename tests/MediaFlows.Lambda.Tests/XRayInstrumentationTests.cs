using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using FluentAssertions;

namespace MediaFlows.Lambda.Tests;

public class XRayInstrumentationTests
{
    [Theory]
    [InlineData(typeof(MediaFlows.Lambda.AnalyticsAggregator.Function))]
    [InlineData(typeof(MediaFlows.Lambda.ContentModerator.Function))]
    [InlineData(typeof(MediaFlows.Lambda.NotificationDispatcher.Function))]
    [InlineData(typeof(MediaFlows.Lambda.PostConfirmationGroupAssigner.Function))]
    [InlineData(typeof(MediaFlows.Lambda.SearchApi.Function))]
    [InlineData(typeof(MediaFlows.Lambda.ThumbnailGenerator.Function))]
    [InlineData(typeof(MediaFlows.Lambda.TrendingApi.Function))]
    public void LambdaFunctionAssembly_ReferencesAWSXRayRecorderHandlersAwsSdk(Type functionType)
    {
        var referenced = functionType.Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToList();

        referenced.Should().Contain("AWSXRayRecorder.Handlers.AwsSdk",
            because: $"{functionType.FullName} must reference the X-Ray AWS SDK handler package " +
                     "to instrument AWS SDK calls under its Lambda invocation segment");
    }

    [Fact]
    public void ContentModerator_StaticInitialization_RegistersXRayPipelineForNewAwsSdkClients()
    {
        // Triggering static initialization (not the instance ctor, which reads
        // env vars the test environment doesn't have set).
        RuntimeHelpers.RunClassConstructor(
            typeof(MediaFlows.Lambda.ContentModerator.Function).TypeHandle);

        var client = new AmazonS3Client(
            new BasicAWSCredentials("test", "test"),
            RegionEndpoint.USEast1);

        var handlers = GetPipelineHandlerTypeNames(client).ToList();
        handlers
            .Should().Contain("XRayPipelineHandler",
                because: "ContentModerator should register X-Ray for the AWS " +
                         "SDK at static initialization so every subsequent " +
                         "client construction inherits the X-Ray handler");
    }

    private static IEnumerable<string> GetPipelineHandlerTypeNames(AmazonServiceClient client)
    {
        Type? type = client.GetType();
        object? pipeline = null;
        while (type != null && pipeline == null)
        {
            pipeline = type.GetProperty("RuntimePipeline",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly)
                ?.GetValue(client);
            if (pipeline == null)
            {
                pipeline = type.GetField("RuntimePipeline",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly)
                    ?.GetValue(client);
            }
            type = type.BaseType;
        }

        if (pipeline == null) yield break;

        var handlersProp = pipeline.GetType().GetProperty("Handlers");
        if (handlersProp?.GetValue(pipeline) is not IEnumerable enumerable) yield break;

        foreach (var handler in enumerable)
        {
            if (handler != null) yield return handler.GetType().Name;
        }
    }
}
