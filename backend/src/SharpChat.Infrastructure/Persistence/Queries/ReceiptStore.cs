using Microsoft.EntityFrameworkCore;
using SharpChat.Application.Abstractions;
using SharpChat.Domain.Conversations;

namespace SharpChat.Infrastructure.Persistence.Queries;

internal sealed class ReceiptStore(SharpChatDbContext db) : IReceiptStore
{
    public Task<ReceiptPosition?> GetAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken) =>
        db.ConversationMembers
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId && m.UserId == userId)
            .Select(m => (ReceiptPosition?)new ReceiptPosition(m.LastDeliveredSeq, m.LastReadSeq))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<long> GetLastSeqAsync(Guid conversationId, CancellationToken cancellationToken) =>
        await db.Messages
            .Where(m => m.ConversationId == conversationId)
            .MaxAsync(m => (long?)m.Seq, cancellationToken) ?? 0;

    public Task AdvanceAsync(Guid conversationId, Guid userId, ReceiptPosition position, CancellationToken cancellationToken) =>
        db.ConversationMembers
            .Where(m => m.ConversationId == conversationId && m.UserId == userId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        m => m.LastDeliveredSeq,
                        m => m.LastDeliveredSeq > position.Delivered ? m.LastDeliveredSeq : position.Delivered)
                    .SetProperty(
                        m => m.LastReadSeq,
                        m => m.LastReadSeq > position.Read ? m.LastReadSeq : position.Read),
                cancellationToken);

    public async Task<IReadOnlyList<Guid>> ListSendersAsync(
        Guid conversationId, long afterSeq, long upToSeq, CancellationToken cancellationToken) =>
        await db.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId && m.Seq > afterSeq && m.Seq <= upToSeq)
            .Select(m => m.SenderId)
            .Distinct()
            .ToListAsync(cancellationToken);
}
