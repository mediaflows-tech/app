using MediaFlows.Shared.Interfaces;

namespace MediaFlows.Web.BackgroundServices;

public class ScheduledPublisherWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScheduledPublisherWorker> _logger;
    private readonly TimeSpan _pollInterval;

    public ScheduledPublisherWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<ScheduledPublisherWorker> logger,
        TimeSpan? pollInterval = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _pollInterval = pollInterval ?? TimeSpan.FromMinutes(1);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ScheduledPublisherWorker started");

        using var timer = new PeriodicTimer(_pollInterval);

        while (!stoppingToken.IsCancellationRequested &&
               await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var reviewService = scope.ServiceProvider.GetRequiredService<IReviewService>();
                var publishedCount = await reviewService.PublishDueScheduledAsync();
                if (publishedCount > 0)
                    _logger.LogInformation("Published {Count} scheduled asset(s)", publishedCount);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to publish scheduled assets");
            }
        }

        _logger.LogInformation("ScheduledPublisherWorker stopped");
    }
}
