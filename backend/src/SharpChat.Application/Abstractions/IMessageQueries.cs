using SharpChat.Application.Messages;

namespace SharpChat.Application.Abstractions;

public interface IMessageQueries
{
    Task<IReadOnlyList<MessageDto>> GetBeforeAsync(
        Guid conversationId, Guid? parentMessageId, MessageCursor? before, int limit, CancellationToken cancellationToken);

    Task<IReadOnlyList<MessageDto>> GetChangesAfterAsync(
        Guid conversationId, long afterChangeSeq, int limit, CancellationToken cancellationToken);

    Task<MessageDto?> GetAsync(Guid messageId, CancellationToken cancellationToken);

    Task<MessageLocation?> GetLocationAsync(Guid messageId, CancellationToken cancellationToken);
}

public sealed record MessageLocation(Guid ConversationId, Guid? ParentMessageId);
