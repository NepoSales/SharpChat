namespace SharpChat.Application.Messages;

public sealed record SendMessageRequest(Guid ConversationId, Guid MessageId, string Content, Guid? ParentMessageId = null);

public sealed record EditMessageRequest(Guid MessageId, string Content);

public sealed record DeleteMessageRequest(Guid MessageId);

public sealed record MessageDto(
    Guid Id,
    Guid ConversationId,
    Guid SenderId,
    string SenderUserName,
    string Content,
    DateTimeOffset SentAt,
    long Seq,
    long ChangeSeq,
    Guid? ParentMessageId,
    int ReplyCount,
    DateTimeOffset? LastReplyAt,
    long? LastReplySeq,
    DateTimeOffset? EditedAt,
    DateTimeOffset? DeletedAt);

public sealed record MessagePage(IReadOnlyList<MessageDto> Items, string? NextCursor);

public sealed record MessageChanges(IReadOnlyList<MessageDto> Items, bool HasMore);

public sealed record MarkReceiptRequest(Guid ConversationId, long Seq);

public sealed record ReceiptDto(Guid ConversationId, Guid UserId, long LastDeliveredSeq, long LastReadSeq);
