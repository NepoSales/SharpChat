using Microsoft.Extensions.Options;
using SharpChat.Application.Abstractions;
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

        var recipients = members.Select(m => m.UserId).ToList();

        var existing = await messages.GetByIdAsync(request.MessageId, cancellationToken);
        if (existing is not null)
        {
            if (!existing.IsSameSend(request.ConversationId, senderId))
            {
                return MessageErrors.IdConflict;
            }

            var retried = ToDto(existing, sender.UserName);
            await notifier.MessageCreatedAsync(retried, recipients, cancellationToken);
            return retried;
        }

        var created = Message.Create(
            request.MessageId,
            request.ConversationId,
            senderId,
            request.Content,
            TruncateToMicroseconds(time.GetUtcNow()),
            options.Value.MaxMessageLength);
        if (!created.IsSuccess)
        {
            return created.Error;
        }

        messages.Add(created.Value);
        await messages.SaveChangesAsync(cancellationToken);

        var dto = ToDto(created.Value, sender.UserName);
        await notifier.MessageCreatedAsync(dto, recipients, cancellationToken);
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
            ? new MessageCursor(page[0].SentAt, page[0].Id).Encode()
            : null;

        return new MessagePage(page, nextCursor);
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
        message.ParentMessageId,
        message.EditedAt,
        message.DeletedAt);
}
