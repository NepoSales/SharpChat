using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using SharpChat.Application.Abstractions;
using SharpChat.Application.Common;
using SharpChat.Domain.Messages;

namespace SharpChat.Infrastructure.Persistence.Repositories;

internal sealed class MessageRepository(SharpChatDbContext db) : IMessageRepository
{
    public async Task<ISequencedWrite> BeginSequencedWriteAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await db.Database.ExecuteSqlAsync(
                $"""SELECT 1 FROM "Conversations" WHERE "Id" = {conversationId} FOR UPDATE""", cancellationToken);

            var lastSeq = await db.Messages
                .Where(m => m.ConversationId == conversationId)
                .MaxAsync(m => (long?)m.ChangeSeq, cancellationToken) ?? 0;

            return new SequencedWrite(transaction, lastSeq + 1);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    public async Task<Message?> GetByIdAsync(Guid messageId, CancellationToken cancellationToken) =>
        await db.Messages.FindAsync([messageId], cancellationToken);

    public void Add(Message message) => db.Messages.Add(message);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            throw new DuplicateKeyException("A message with the same key already exists.", ex);
        }
    }

    private sealed class SequencedWrite(IDbContextTransaction transaction, long nextSeq) : ISequencedWrite
    {
        public long NextSeq => nextSeq;

        public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
