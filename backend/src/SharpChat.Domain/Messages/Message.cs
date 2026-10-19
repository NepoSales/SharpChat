using SharpChat.Domain.Common;

namespace SharpChat.Domain.Messages;

public sealed class Message
{
    private Message()
    {
    }

    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid SenderId { get; private set; }
    public string Content { get; private set; } = null!;
    public DateTimeOffset SentAt { get; private set; }

    public Guid? ParentMessageId { get; private set; }

    public DateTimeOffset? EditedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    public static Result<Message> Create(
        Guid id,
        Guid conversationId,
        Guid senderId,
        string content,
        DateTimeOffset sentAt,
        int maxContentLength)
    {
        if (id == Guid.Empty)
        {
            return MessageErrors.InvalidId;
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            return MessageErrors.EmptyContent;
        }

        if (content.Length > maxContentLength)
        {
            return MessageErrors.ContentTooLong(maxContentLength);
        }

        return new Message
        {
            Id = id,
            ConversationId = conversationId,
            SenderId = senderId,
            Content = content,
            SentAt = sentAt,
        };
    }

    public bool IsSameSend(Guid conversationId, Guid senderId) => ConversationId == conversationId && SenderId == senderId;
}
