using Microsoft.EntityFrameworkCore;
using SharpChat.Application.Abstractions;
using SharpChat.Domain.Attachments;

namespace SharpChat.Infrastructure.Persistence.Repositories;

internal sealed class AttachmentRepository(SharpChatDbContext db) : IAttachmentRepository
{
    public async Task<Attachment?> GetByIdAsync(Guid attachmentId, CancellationToken cancellationToken) =>
        await db.Attachments.FindAsync([attachmentId], cancellationToken);

    public async Task<IReadOnlyList<Attachment>> GetByIdsAsync(
        IReadOnlyCollection<Guid> attachmentIds, CancellationToken cancellationToken) =>
        await db.Attachments.Where(a => attachmentIds.Contains(a.Id)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Attachment>> GetByMessageIdAsync(Guid messageId, CancellationToken cancellationToken) =>
        await db.Attachments.Where(a => a.MessageId == messageId).ToListAsync(cancellationToken);

    public void Add(Attachment attachment) => db.Attachments.Add(attachment);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
