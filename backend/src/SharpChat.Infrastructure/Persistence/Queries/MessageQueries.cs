using Microsoft.EntityFrameworkCore;
using SharpChat.Application.Abstractions;
using SharpChat.Application.Messages;

namespace SharpChat.Infrastructure.Persistence.Queries;

internal sealed class MessageQueries(SharpChatDbContext db) : IMessageQueries
{
    public async Task<IReadOnlyList<MessageDto>> GetBeforeAsync(
        Guid conversationId, MessageCursor? before, int limit, CancellationToken cancellationToken)
    {
        var query = db.Messages.AsNoTracking().Where(m => m.ConversationId == conversationId);

        if (before is not null)
        {
            query = query.Where(m => EF.Functions.LessThan(
                ValueTuple.Create(m.SentAt, m.Id),
                ValueTuple.Create(before.SentAt, before.Id)));
        }

        return await query
            .OrderByDescending(m => m.SentAt)
            .ThenByDescending(m => m.Id)
            .Take(limit)
            .Join(db.Users, m => m.SenderId, u => u.Id, (m, u) => new MessageDto(
                m.Id, m.ConversationId, m.SenderId, u.UserName!, m.Content, m.SentAt, m.ParentMessageId, m.EditedAt, m.DeletedAt))
            .ToListAsync(cancellationToken);
    }
}
