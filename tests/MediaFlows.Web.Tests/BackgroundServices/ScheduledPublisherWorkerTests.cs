using MediaFlows.Shared.Interfaces;
using MediaFlows.Web.BackgroundServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace MediaFlows.Web.Tests.BackgroundServices;

public class ScheduledPublisherWorkerTests
{
    private static (ScheduledPublisherWorker worker,
                    Mock<IReviewService> reviewService,
                    Mock<ILogger<ScheduledPublisherWorker>> logger) CreateWorker()
    {
        var reviewServiceMock = new Mock<IReviewService>();
        reviewServiceMock.Setup(r => r.PublishDueScheduledAsync()).ReturnsAsync(0);

        var providerMock = new Mock<IServiceProvider>();
        providerMock.Setup(p => p.GetService(typeof(IReviewService)))
            .Returns(reviewServiceMock.Object);

        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(providerMock.Object);

        var factoryMock = new Mock<IServiceScopeFactory>();
        factoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

        var logger = new Mock<ILogger<ScheduledPublisherWorker>>();

        var worker = new ScheduledPublisherWorker(
            factoryMock.Object, logger.Object, TimeSpan.FromMilliseconds(20));

        return (worker, reviewServiceMock, logger);
    }

    [Fact]
    public async Task ExecuteAsync_InvokesPublishDueScheduled_OnTick()
    {
        var (worker, reviewService, _) = CreateWorker();

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(150));

        await worker.StartAsync(cts.Token);
        await Task.Delay(100);
        await worker.StopAsync(CancellationToken.None);

        reviewService.Verify(r => r.PublishDueScheduledAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_StartsAndStopsGracefully()
    {
        var (worker, _, logger) = CreateWorker();

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        await worker.StartAsync(cts.Token);
        await Task.Delay(50);
        await worker.StopAsync(CancellationToken.None);

        logger.Verify(l => l.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("started")),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_HandlesServiceExceptionsGracefully()
    {
        var (worker, reviewService, _) = CreateWorker();
        reviewService.Setup(r => r.PublishDueScheduledAsync())
            .ThrowsAsync(new InvalidOperationException("DB down"));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(150));

        // Should not throw even when the review service throws.
        await worker.StartAsync(cts.Token);
        await Task.Delay(100);
        await worker.StopAsync(CancellationToken.None);
    }
}
