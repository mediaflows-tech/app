using MediaFlows.Shared.DTOs;
using MediaFlows.Shared.Interfaces;
using MediaFlows.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace MediaFlows.Web.BackgroundServices;

public class AnalyticsWorker : BackgroundService
{
    private readonly IHubContext<AnalyticsHub, IAnalyticsClient> _analyticsHub;
    private readonly IHubContext<NotificationHub, INotificationClient> _notificationHub;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AnalyticsWorker> _logger;
    private HashSet<string> _previousAlarmNames = new();

    private static string FormatAlarmName(string name)
    {
        // EB auto-generated: awseb-e-{id}-stack-AWSEBCloudwatchAlarm{High|Low}-{hash}
        var ebMatch = System.Text.RegularExpressions.Regex.Match(name, @"AWSEBCloudwatchAlarm(High|Low)-", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (ebMatch.Success)
            return $"EB Auto-Scaling ({ebMatch.Groups[1].Value})";

        // Terraform-defined: mediaflows-{name}-{env}
        var formatted = System.Text.RegularExpressions.Regex.Replace(name, @"^mediaflows-", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        formatted = System.Text.RegularExpressions.Regex.Replace(formatted, @"-(prod|dev|staging)$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(formatted.Replace('-', ' '));
    }

    public AnalyticsWorker(
        IHubContext<AnalyticsHub, IAnalyticsClient> analyticsHub,
        IHubContext<NotificationHub, INotificationClient> notificationHub,
        IServiceScopeFactory scopeFactory,
        ILogger<AnalyticsWorker> logger)
    {
        _analyticsHub = analyticsHub;
        _notificationHub = notificationHub;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AnalyticsWorker started");

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        while (!stoppingToken.IsCancellationRequested &&
               await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var analytics = scope.ServiceProvider.GetRequiredService<IAnalyticsService>();
                var snapshot = await analytics.GetCurrentSnapshotAsync();
                await _analyticsHub.Clients.All.ReceiveAnalyticsUpdate(snapshot);
                await CheckAlarmChangesAsync(snapshot.ActiveAlarms);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to push analytics update");
            }
        }

        _logger.LogInformation("AnalyticsWorker stopped");
    }

    private async Task CheckAlarmChangesAsync(List<CloudWatchAlarmDto> currentAlarms)
    {
        var currentNames = currentAlarms.Select(a => a.AlarmName).ToHashSet();

        var newAlarms = currentAlarms
            .Where(a => !_previousAlarmNames.Contains(a.AlarmName))
            .ToList();

        var resolvedNames = _previousAlarmNames
            .Where(name => !currentNames.Contains(name))
            .ToList();

        var admins = _notificationHub.Clients.Group("role_SystemAdmin");

        foreach (var alarm in newAlarms)
        {
            var friendly = FormatAlarmName(alarm.AlarmName);
            _logger.LogWarning("CloudWatch alarm triggered: {AlarmName}", alarm.AlarmName);

            await admins.ReceiveToast(
                "CloudWatch Alarm",
                $"{friendly} — {alarm.MetricName} is in ALARM state",
                "warning");

            await admins.ReceiveNotification(
                $"""
                <div class="notification-item unread">
                    <div class="d-flex align-items-start gap-2">
                        <i class="bi bi-exclamation-triangle-fill text-warning mt-1"></i>
                        <div class="flex-grow-1">
                            <div class="fw-semibold" style="font-size: 0.875rem;">Active alarm: {friendly}</div>
                            <div class="small text-muted">{alarm.MetricName} — Status: {alarm.StateValue}</div>
                            <div class="text-muted" style="font-size: 0.75rem;">Last updated {alarm.StateUpdatedTimestamp:MMM d, HH:mm}</div>
                        </div>
                        <button class="btn-icon notification-dismiss" type="button" style="flex-shrink:0;font-size:0.75rem;" aria-label="Dismiss"><i class="bi bi-x"></i></button>
                    </div>
                </div>
                """);
        }

        foreach (var name in resolvedNames)
        {
            var friendly = FormatAlarmName(name);
            _logger.LogInformation("CloudWatch alarm resolved: {AlarmName}", name);

            await admins.ReceiveToast(
                "Alarm Resolved",
                $"{friendly} is no longer in ALARM state",
                "success");

            await admins.ReceiveNotification(
                $"""
                <div class="notification-item unread">
                    <div class="d-flex align-items-start gap-2">
                        <i class="bi bi-check-circle-fill text-success mt-1"></i>
                        <div class="flex-grow-1">
                            <div class="fw-semibold" style="font-size: 0.875rem;">Resolved: {friendly}</div>
                            <div class="small text-muted">Alarm is no longer active</div>
                            <div class="text-muted" style="font-size: 0.75rem;">{DateTime.UtcNow:MMM d, HH:mm}</div>
                        </div>
                        <button class="btn-icon notification-dismiss" type="button" style="flex-shrink:0;font-size:0.75rem;" aria-label="Dismiss"><i class="bi bi-x"></i></button>
                    </div>
                </div>
                """);
        }

        _previousAlarmNames = currentNames;
    }
}
