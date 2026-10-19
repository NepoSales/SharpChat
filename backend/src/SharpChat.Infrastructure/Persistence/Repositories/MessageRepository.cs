using SharpChat.Application.Abstractions;
using SharpChat.Domain.Messages;

namespace SharpChat.Infrastructure.Persistence.Repositories;

internal sealed class MessageRepository(SharpChatDbContext db) : IMessageRepository
{
    public async Task<Message?> GetByIdAsync(Guid messageId, CancellationToken cancellationToken) =>
        await db.Messages.FindAsync([messageId], cancellationToken);

    public void Add(Message message) => db.Messages.Add(message);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
