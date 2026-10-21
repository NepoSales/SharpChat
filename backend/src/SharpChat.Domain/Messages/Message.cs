using SharpChat.Domain.Common;
using SharpChat.Domain.Conversations;

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

    public long Seq { get; private set; }
    public long ChangeSeq { get; private set; }

    public Guid? ParentMessageId { get; private set; }
    public int ReplyCount { get; private set; }
    public DateTimeOffset? LastReplyAt { get; private set; }
    public long? LastReplySeq { get; private set; }

    public DateTimeOffset? EditedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    public bool IsDeleted => DeletedAt is not null;

    public static Result<Message> Create(
        Guid id,
        Guid conversationId,
        Guid senderId,
        string content,
        DateTimeOffset sentAt,
        long seq,
        int maxContentLength)
    {
        if (id == Guid.Empty)
        {
            return MessageErrors.InvalidId;
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(seq);

        var invalid = ValidateContent(content, maxContentLength);
        if (invalid is not null)
        {
            return invalid;
        }

        return new Message
        {
            Id = id,
            ConversationId = conversationId,
            SenderId = senderId,
            Content = content,
            SentAt = sentAt,
            Seq = seq,
            ChangeSeq = seq,
        };
    }

    public Result<Message> Reply(
        Guid id,
        Guid senderId,
        string content,
        DateTimeOffset sentAt,
        long seq,
        long parentChangeSeq,
        int maxContentLength)
    {
        if (ParentMessageId is not null)
        {
            return MessageErrors.NestedReply;
        }

        if (IsDeleted)
        {
            return MessageErrors.Deleted;
        }

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(parentChangeSeq, seq);

        var created = Create(id, ConversationId, senderId, content, sentAt, seq, maxContentLength);
        if (!created.IsSuccess)
        {
            return created;
        }

        created.Value.ParentMessageId = Id;
        ReplyCount++;
        LastReplyAt = sentAt;
        LastReplySeq = seq;
        ChangeSeq = parentChangeSeq;
        return created;
    }

    public Result Edit(Guid actorId, string content, DateTimeOffset now, long changeSeq, int maxContentLength)
    {
        if (IsDeleted)
        {
            return MessageErrors.Deleted;
        }

        if (actorId != SenderId)
        {
            return MessageErrors.NotAuthor;
        }

        var invalid = ValidateContent(content, maxContentLength);
        if (invalid is not null)
        {
            return invalid;
        }

        Content = content;
        EditedAt = now;
        ChangeSeq = changeSeq;
        return Result.Success();
    }

    public Result Delete(Guid actorId, ConversationRole actorRole, DateTimeOffset now, long changeSeq)
    {
        if (actorId != SenderId && actorRole < ConversationRole.Admin)
        {
            return MessageErrors.CannotDelete;
        }

        if (IsDeleted)
        {
            return Result.Success();
        }

        Content = string.Empty;
        DeletedAt = now;
        ChangeSeq = changeSeq;
        return Result.Success();
    }

    public bool IsSameSend(Guid conversationId, Guid senderId, Guid? parentMessageId) =>
        ConversationId == conversationId && SenderId == senderId && ParentMessageId == parentMessageId;

    private static Error? ValidateContent(string content, int maxContentLength)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return MessageErrors.EmptyContent;
        }

        return content.Length > maxContentLength ? MessageErrors.ContentTooLong(maxContentLength) : null;
    }
}
