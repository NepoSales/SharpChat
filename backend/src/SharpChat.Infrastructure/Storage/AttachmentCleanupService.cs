using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SharpChat.Infrastructure.Storage;

internal sealed partial class AttachmentCleanupService(
    IServiceScopeFactory scopes,
    IOptions<StorageOptions> options,
    TimeProvider time,
    ILogger<AttachmentCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.CleanupEnabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(options.Value.CleanupInterval, time);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var removed = await scope.ServiceProvider.GetRequiredService<AttachmentCleanup>().RunOnceAsync(stoppingToken);
                if (removed > 0)
                {
                    LogRemoved(removed);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogFailed(ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Attachment cleanup removed {Count} files")]
    private partial void LogRemoved(int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Attachment cleanup failed")]
    private partial void LogFailed(Exception exception);
}
