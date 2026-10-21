using Microsoft.Extensions.Options;
using SharpChat.Application.Abstractions;
using SharpChat.Application.Common;
using SharpChat.Application.Conversations;
using SharpChat.Application.Diagnostics;
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
    ChatMetrics metrics,
    TimeProvider time)
{
    public async Task<Result<MessageDto>> SendAsync(Guid senderId, SendMessageRequest request, CancellationToken cancellationToken)
    {
        var started = time.GetTimestamp();
        var (result, outcome) = await SendCoreAsync(senderId, request, cancellationToken);
        metrics.SendCompleted(outcome, time.GetElapsedTime(started));
        return result;
    }

    public Task<Result<MessageDto>> EditAsync(Guid actorId, EditMessageRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(
            actorId,
            request.MessageId,
            MessageChange.Edited,
            (message, _, changeSeq) => message.Edit(
                actorId, request.Content, TruncateToMicroseconds(time.GetUtcNow()), changeSeq, options.Value.MaxMessageLength),
            cancellationToken);

    public Task<Result<MessageDto>> DeleteAsync(Guid actorId, DeleteMessageRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(
            actorId,
            request.MessageId,
            MessageChange.Deleted,
            (message, role, changeSeq) => message.Delete(actorId, role, TruncateToMicroseconds(time.GetUtcNow()), changeSeq),
            cancellationToken);

    public async Task<Result<MessagePage>> GetHistoryAsync(
        Guid conversationId, string? before, int? limit, CancellationToken cancellationToken)
    {
        if (!await membership.IsMemberAsync(conversationId, currentUser.RequiredUserId(), cancellationToken))
        {
            return ConversationErrors.NotFound;
        }

        return await PageAsync(conversationId, parentMessageId: null, before, limit, cancellationToken);
    }

    public async Task<Result<MessageDto>> GetAsync(Guid conversationId, Guid messageId, CancellationToken cancellationToken)
    {
        if (!await membership.IsMemberAsync(conversationId, currentUser.RequiredUserId(), cancellationToken))
        {
            return ConversationErrors.NotFound;
        }

        var message = await messageQueries.GetAsync(messageId, cancellationToken);
        return message is not null && message.ConversationId == conversationId ? message : MessageErrors.NotFound;
    }

    public async Task<Result<MessagePage>> GetRepliesAsync(
        Guid conversationId, Guid messageId, string? before, int? limit, CancellationToken cancellationToken)
    {
        if (!await membership.IsMemberAsync(conversationId, currentUser.RequiredUserId(), cancellationToken))
        {
            return ConversationErrors.NotFound;
        }

        var location = await messageQueries.GetLocationAsync(messageId, cancellationToken);
        if (location is null || location.ConversationId != conversationId || location.ParentMessageId is not null)
        {
            return MessageErrors.NotFound;
        }

        return await PageAsync(conversationId, messageId, before, limit, cancellationToken);
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

    private async Task<Result<MessagePage>> PageAsync(
        Guid conversationId, Guid? parentMessageId, string? before, int? limit, CancellationToken cancellationToken)
    {
        MessageCursor? cursor = null;
        if (before is not null && !MessageCursor.TryDecode(before, out cursor))
        {
            return MessageErrors.InvalidCursor;
        }

        var pageSize = Math.Clamp(limit ?? options.Value.DefaultHistoryPageSize, 1, ChatOptions.MaxHistoryPageSize);

        var newestFirst = await messageQueries.GetBeforeAsync(
            conversationId, parentMessageId, cursor, pageSize + 1, cancellationToken);
        var page = newestFirst.Take(pageSize).Reverse().ToList();

        var nextCursor = newestFirst.Count > pageSize
            ? new MessageCursor(page[0].Seq).Encode()
            : null;

        return new MessagePage(page, nextCursor);
    }

    private async Task<(Result<MessageDto> Result, SendOutcome Outcome)> SendCoreAsync(
        Guid senderId, SendMessageRequest request, CancellationToken cancellationToken)
    {
        var members = await conversationQueries.ListMembersAsync(request.ConversationId, cancellationToken);
        var sender = members.FirstOrDefault(m => m.UserId == senderId);
        if (sender is null)
        {
            return (ConversationErrors.NotFound, SendOutcome.Rejected);
        }

        var stored = await StoreAsync(senderId, request, cancellationToken);
        if (!stored.IsSuccess)
        {
            return (stored.Error, SendOutcome.Rejected);
        }

        var (message, parent, created) = stored.Value;
        var recipients = members.Select(m => m.UserId).ToList();
        var dto = ToDto(message, sender.UserName);
        await notifier.MessageCreatedAsync(dto, recipients, cancellationToken);
        if (parent is not null && await messageQueries.GetAsync(parent.Id, cancellationToken) is { } parentDto)
        {
            await notifier.MessageUpdatedAsync(parentDto, recipients, cancellationToken);
        }

        if (!created)
        {
            return (dto, SendOutcome.Duplicate);
        }

        metrics.MessageSent(isReply: message.ParentMessageId is not null);
        return (dto, SendOutcome.Created);
    }

    private async Task<Result<(Message Message, Message? Parent, bool Created)>> StoreAsync(
        Guid senderId, SendMessageRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await using var write = await messages.BeginSequencedWriteAsync(request.ConversationId, cancellationToken);

            var existing = await messages.GetByIdAsync(request.MessageId, cancellationToken);
            if (existing is not null)
            {
                return existing.IsSameSend(request.ConversationId, senderId, request.ParentMessageId)
                    ? (existing, null, false)
                    : MessageErrors.IdConflict;
            }

            var sentAt = TruncateToMicroseconds(time.GetUtcNow());
            var maxLength = options.Value.MaxMessageLength;
            Message? parent = null;
            Result<Message> created;

            if (request.ParentMessageId is { } parentId)
            {
                parent = await messages.GetByIdAsync(parentId, cancellationToken);
                if (parent is null || parent.ConversationId != request.ConversationId)
                {
                    return MessageErrors.ParentNotFound;
                }

                var replySeq = write.TakeSeq();
                created = parent.Reply(request.MessageId, senderId, request.Content, sentAt, replySeq, write.TakeSeq(), maxLength);
            }
            else
            {
                created = Message.Create(
                    request.MessageId, request.ConversationId, senderId, request.Content, sentAt, write.TakeSeq(), maxLength);
            }

            if (!created.IsSuccess)
            {
                return created.Error;
            }

            messages.Add(created.Value);
            await messages.SaveChangesAsync(cancellationToken);
            await write.CommitAsync(cancellationToken);
            return (created.Value, parent, true);
        }
        catch (DuplicateKeyException)
        {
            return MessageErrors.IdConflict;
        }
    }

    private async Task<Result<MessageDto>> ChangeAsync(
        Guid actorId,
        Guid messageId,
        MessageChange kind,
        Func<Message, ConversationRole, long, Result> change,
        CancellationToken cancellationToken)
    {
        var location = await messageQueries.GetLocationAsync(messageId, cancellationToken);
        if (location is null)
        {
            return MessageErrors.NotFound;
        }

        var members = await conversationQueries.ListMembersAsync(location.ConversationId, cancellationToken);
        var actor = members.FirstOrDefault(m => m.UserId == actorId);
        if (actor is null)
        {
            return MessageErrors.NotFound;
        }

        var changed = false;
        await using (var write = await messages.BeginSequencedWriteAsync(location.ConversationId, cancellationToken))
        {
            var message = (await messages.GetByIdAsync(messageId, cancellationToken))!;
            var changeSeqBefore = message.ChangeSeq;

            var result = change(message, actor.Role, write.TakeSeq());
            if (!result.IsSuccess)
            {
                return result.Error;
            }

            if (message.ChangeSeq != changeSeqBefore)
            {
                await messages.SaveChangesAsync(cancellationToken);
                await write.CommitAsync(cancellationToken);
                changed = true;
                metrics.MessageChanged(kind);
            }
        }

        var dto = (await messageQueries.GetAsync(messageId, cancellationToken))!;
        if (changed)
        {
            await notifier.MessageUpdatedAsync(dto, members.Select(m => m.UserId).ToList(), cancellationToken);
        }

        return dto;
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
        message.ReplyCount,
        message.LastReplyAt,
        message.LastReplySeq,
        message.EditedAt,
        message.DeletedAt);
}
