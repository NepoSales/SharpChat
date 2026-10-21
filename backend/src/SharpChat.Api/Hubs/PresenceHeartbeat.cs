using Microsoft.Extensions.Options;
using SharpChat.Application.Abstractions;
using SharpChat.Application.Presence;

namespace SharpChat.Api.Hubs;

internal sealed partial class PresenceHeartbeat(
    ConnectionRegistry registry,
    IPresenceTracker tracker,
    IOptions<PresenceOptions> options,
    TimeProvider time,
    ILogger<PresenceHeartbeat> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.HeartbeatSeconds), time);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await tracker.RefreshAsync(registry.Snapshot(), stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogRefreshFailed(ex);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not refresh presence; connections will expire if this keeps failing.")]
    private partial void LogRefreshFailed(Exception exception);
}
