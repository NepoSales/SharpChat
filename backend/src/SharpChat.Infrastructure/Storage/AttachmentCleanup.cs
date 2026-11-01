using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharpChat.Application.Abstractions;
using SharpChat.Application.Conversations;
using SharpChat.Infrastructure.Persistence;

namespace SharpChat.Infrastructure.Storage;

public sealed partial class AttachmentCleanup(
    SharpChatDbContext db,
    IFileStorage storage,
    IOptions<ChatOptions> options,
    TimeProvider time,
    ILogger<AttachmentCleanup> logger)
{
    private const int BatchSize = 100;

    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        var removed = 0;
        while (true)
        {
            var pendingBefore = time.GetUtcNow() - options.Value.PendingAttachmentLifetime;
            var batch = await db.Attachments
                .AsNoTracking()
                .Where(a => a.DeletedAt != null || (a.MessageId == null && a.UploadedAt < pendingBefore))
                .OrderBy(a => a.UploadedAt)
                .Take(BatchSize)
                .Select(a => new { a.Id, a.StorageKey })
                .ToListAsync(cancellationToken);

            foreach (var item in batch)
            {
                var deleted = await db.Attachments
                    .Where(a => a.Id == item.Id
                        && (a.DeletedAt != null || (a.MessageId == null && a.UploadedAt < pendingBefore)))
                    .ExecuteDeleteAsync(cancellationToken);
                if (deleted == 0)
                {
                    continue;
                }

                try
                {
                    await storage.DeleteAsync(item.StorageKey, cancellationToken);
                }
                catch (IOException ex)
                {
                    LogFileNotDeleted(ex, item.StorageKey);
                }

                removed++;
            }

            if (batch.Count < BatchSize)
            {
                return removed;
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Attachment file {StorageKey} could not be deleted")]
    private partial void LogFileNotDeleted(Exception exception, string storageKey);
}
