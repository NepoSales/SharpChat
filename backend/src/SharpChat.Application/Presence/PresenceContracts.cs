namespace SharpChat.Application.Presence;

public sealed record PresenceDto(Guid UserId, bool IsOnline);

public sealed record TypingRequest(Guid ConversationId, Guid? ParentMessageId = null);

public sealed record TypingDto(Guid ConversationId, Guid? ParentMessageId, Guid UserId, string UserName);
