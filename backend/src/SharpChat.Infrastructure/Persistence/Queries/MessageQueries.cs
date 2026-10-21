using Microsoft.EntityFrameworkCore;
using SharpChat.Application.Abstractions;
using SharpChat.Application.Messages;
using SharpChat.Domain.Messages;

namespace SharpChat.Infrastructure.Persistence.Queries;

internal sealed class MessageQueries(SharpChatDbContext db) : IMessageQueries
{
    public async Task<IReadOnlyList<MessageDto>> GetBeforeAsync(
        Guid conversationId, MessageCursor? before, int limit, CancellationToken cancellationToken)
    {
        var query = db.Messages.AsNoTracking().Where(m => m.ConversationId == conversationId);

        if (before is not null)
        {
            query = query.Where(m => m.Seq < before.Seq);
        }

        return await ToDtos(query.OrderByDescending(m => m.Seq).Take(limit)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MessageDto>> GetChangesAfterAsync(
        Guid conversationId, long afterChangeSeq, int limit, CancellationToken cancellationToken)
    {
        var query = db.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId && m.ChangeSeq > afterChangeSeq)
            .OrderBy(m => m.ChangeSeq)
            .Take(limit);

        return await ToDtos(query).ToListAsync(cancellationToken);
    }

    private IQueryable<MessageDto> ToDtos(IQueryable<Message> messages) =>
        messages.Join(db.Users, m => m.SenderId, u => u.Id, (m, u) => new MessageDto(
            m.Id,
            m.ConversationId,
            m.SenderId,
            u.UserName!,
            m.Content,
            m.SentAt,
            m.Seq,
            m.ChangeSeq,
            m.ParentMessageId,
            m.EditedAt,
            m.DeletedAt));
}
