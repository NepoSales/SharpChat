namespace SharpChat.Application.Messages;

public sealed record SendMessageRequest(Guid ConversationId, Guid MessageId, string Content);

public sealed record MessageDto(
    Guid Id,
    Guid ConversationId,
    Guid SenderId,
    string SenderUserName,
    string Content,
    DateTimeOffset SentAt,
    Guid? ParentMessageId,
    DateTimeOffset? EditedAt,
    DateTimeOffset? DeletedAt);

public sealed record MessagePage(IReadOnlyList<MessageDto> Items, string? NextCursor);
