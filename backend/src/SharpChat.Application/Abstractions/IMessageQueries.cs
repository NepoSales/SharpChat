using SharpChat.Application.Messages;

namespace SharpChat.Application.Abstractions;

public interface IMessageQueries
{
    Task<IReadOnlyList<MessageDto>> GetBeforeAsync(
        Guid conversationId, MessageCursor? before, int limit, CancellationToken cancellationToken);
}
