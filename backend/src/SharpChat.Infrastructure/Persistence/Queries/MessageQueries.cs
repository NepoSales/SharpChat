using Microsoft.EntityFrameworkCore;
using SharpChat.Application.Abstractions;
using SharpChat.Application.Attachments;
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

        return await WithAttachmentsAsync(
            await ToDtos(query.OrderByDescending(m => m.Seq).Take(limit)).ToListAsync(cancellationToken), cancellationToken);
    }

    public async Task<IReadOnlyList<MessageDto>> GetChangesAfterAsync(
        Guid conversationId, long afterChangeSeq, int limit, CancellationToken cancellationToken)
    {
        var query = db.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId && m.ChangeSeq > afterChangeSeq)
            .OrderBy(m => m.ChangeSeq)
            .Take(limit);

        return await WithAttachmentsAsync(await ToDtos(query).ToListAsync(cancellationToken), cancellationToken);
    }

    public async Task<MessageDto?> GetAsync(Guid messageId, CancellationToken cancellationToken)
    {
        var message = await ToDtos(db.Messages.AsNoTracking().Where(m => m.Id == messageId)).FirstOrDefaultAsync(cancellationToken);
        return message is null ? null : (await WithAttachmentsAsync([message], cancellationToken))[0];
    }

    public Task<MessageLocation?> GetLocationAsync(Guid messageId, CancellationToken cancellationToken) =>
        db.Messages
            .AsNoTracking()
            .Where(m => m.Id == messageId)
            .Select(m => new MessageLocation(m.ConversationId, m.ParentMessageId))
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<List<MessageDto>> WithAttachmentsAsync(List<MessageDto> messages, CancellationToken cancellationToken)
    {
        var ids = messages.Where(m => m.DeletedAt is null).Select(m => m.Id).ToList();
        if (ids.Count == 0)
        {
            return messages;
        }

        var attachments = await db.Attachments
            .AsNoTracking()
            .Where(a => a.MessageId != null && ids.Contains(a.MessageId.Value) && a.DeletedAt == null)
            .OrderBy(a => a.UploadedAt)
            .ThenBy(a => a.Id)
            .Select(a => new
            {
                MessageId = a.MessageId!.Value,
                Dto = new AttachmentDto(a.Id, a.FileName, a.ContentType, a.Kind, a.SizeBytes),
            })
            .ToListAsync(cancellationToken);

        if (attachments.Count == 0)
        {
            return messages;
        }

        var byMessage = attachments.ToLookup(a => a.MessageId, a => a.Dto);
        return messages
            .Select(m => byMessage.Contains(m.Id) ? m with { Attachments = byMessage[m.Id].ToList() } : m)
            .ToList();
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
            m.ReplyCount,
            m.LastReplyAt,
            m.LastReplySeq,
            m.EditedAt,
            m.DeletedAt,
            Array.Empty<AttachmentDto>()));
}
