using Microsoft.EntityFrameworkCore;
using SharpChat.Application.Abstractions;
using SharpChat.Application.Messages;
using SharpChat.Domain.Messages;

namespace SharpChat.Infrastructure.Persistence.Queries;

internal sealed class MessageQueries(SharpChatDbContext db) : IMessageQueries
{
    public async Task<IReadOnlyList<MessageDto>> GetBeforeAsync(
        Guid conversationId, Guid? parentMessageId, MessageCursor? before, int limit, CancellationToken cancellationToken)
    {
        var query = db.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId && m.ParentMessageId == parentMessageId);

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

    public Task<MessageDto?> GetAsync(Guid messageId, CancellationToken cancellationToken) =>
        ToDtos(db.Messages.AsNoTracking().Where(m => m.Id == messageId)).FirstOrDefaultAsync(cancellationToken);

    public Task<MessageLocation?> GetLocationAsync(Guid messageId, CancellationToken cancellationToken) =>
        db.Messages
            .AsNoTracking()
            .Where(m => m.Id == messageId)
            .Select(m => new MessageLocation(m.ConversationId, m.ParentMessageId))
            .FirstOrDefaultAsync(cancellationToken);

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
            m.ReplyCount,
            m.LastReplyAt,
            m.LastReplySeq,
            m.EditedAt,
            m.DeletedAt));
}
