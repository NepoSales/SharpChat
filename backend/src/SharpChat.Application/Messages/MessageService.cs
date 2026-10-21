using Microsoft.Extensions.Options;
using SharpChat.Application.Abstractions;
using SharpChat.Application.Common;
using SharpChat.Application.Conversations;
using SharpChat.Domain.Common;
using SharpChat.Domain.Conversations;
using SharpChat.Domain.Messages;

namespace SharpChat.Application.Messages;

public sealed class MessageService(
    IMessageRepository messages,
    IMessageQueries messageQueries,
    IConversationQueries conversationQueries,
    MembershipService membership,
    IChatNotifier notifier,
    ICurrentUser currentUser,
    IOptions<ChatOptions> options,
    TimeProvider time)
{
    public async Task<Result<MessageDto>> SendAsync(Guid senderId, SendMessageRequest request, CancellationToken cancellationToken)
    {
        var members = await conversationQueries.ListMembersAsync(request.ConversationId, cancellationToken);
        var sender = members.FirstOrDefault(m => m.UserId == senderId);
        if (sender is null)
        {
            return ConversationErrors.NotFound;
        }

        var stored = await StoreAsync(senderId, request, cancellationToken);
        if (!stored.IsSuccess)
        {
            return stored.Error;
        }

        var dto = ToDto(stored.Value, sender.UserName);
        await notifier.MessageCreatedAsync(dto, members.Select(m => m.UserId).ToList(), cancellationToken);
        return dto;
    }

    public async Task<Result<MessagePage>> GetHistoryAsync(
        Guid conversationId, string? before, int? limit, CancellationToken cancellationToken)
    {
        if (!await membership.IsMemberAsync(conversationId, currentUser.RequiredUserId(), cancellationToken))
        {
            return ConversationErrors.NotFound;
        }

        MessageCursor? cursor = null;
        if (before is not null && !MessageCursor.TryDecode(before, out cursor))
        {
            return MessageErrors.InvalidCursor;
        }

        var pageSize = Math.Clamp(limit ?? options.Value.DefaultHistoryPageSize, 1, ChatOptions.MaxHistoryPageSize);

        var newestFirst = await messageQueries.GetBeforeAsync(conversationId, cursor, pageSize + 1, cancellationToken);
        var page = newestFirst.Take(pageSize).Reverse().ToList();

        var nextCursor = newestFirst.Count > pageSize
            ? new MessageCursor(page[0].Seq).Encode()
            : null;

        return new MessagePage(page, nextCursor);
    }

    public async Task<Result<MessageChanges>> GetChangesAsync(
        Guid conversationId, long after, int? limit, CancellationToken cancellationToken)
    {
        if (!await membership.IsMemberAsync(conversationId, currentUser.RequiredUserId(), cancellationToken))
        {
            return ConversationErrors.NotFound;
        }

        if (after < 0)
        {
            return MessageErrors.InvalidSeq;
        }

        var pageSize = Math.Clamp(limit ?? ChatOptions.MaxChangesPageSize, 1, ChatOptions.MaxChangesPageSize);
        var changes = await messageQueries.GetChangesAfterAsync(conversationId, after, pageSize + 1, cancellationToken);

        return new MessageChanges(changes.Take(pageSize).ToList(), changes.Count > pageSize);
    }

    private async Task<Result<Message>> StoreAsync(Guid senderId, SendMessageRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await using var write = await messages.BeginSequencedWriteAsync(request.ConversationId, cancellationToken);

            var existing = await messages.GetByIdAsync(request.MessageId, cancellationToken);
            if (existing is not null)
            {
                return existing.IsSameSend(request.ConversationId, senderId) ? existing : MessageErrors.IdConflict;
            }

            var created = Message.Create(
                request.MessageId,
                request.ConversationId,
                senderId,
                request.Content,
                TruncateToMicroseconds(time.GetUtcNow()),
                write.NextSeq,
                options.Value.MaxMessageLength);
            if (!created.IsSuccess)
            {
                return created.Error;
            }

            messages.Add(created.Value);
            await messages.SaveChangesAsync(cancellationToken);
            await write.CommitAsync(cancellationToken);
            return created.Value;
        }
        catch (DuplicateKeyException)
        {
            return MessageErrors.IdConflict;
        }
    }

    private static DateTimeOffset TruncateToMicroseconds(DateTimeOffset value) =>
        new(value.UtcTicks - (value.UtcTicks % TimeSpan.TicksPerMicrosecond), TimeSpan.Zero);

    private static MessageDto ToDto(Message message, string senderUserName) => new(
        message.Id,
        message.ConversationId,
        message.SenderId,
        senderUserName,
        message.Content,
        message.SentAt,
        message.Seq,
        message.ChangeSeq,
        message.ParentMessageId,
        message.EditedAt,
        message.DeletedAt);
}
