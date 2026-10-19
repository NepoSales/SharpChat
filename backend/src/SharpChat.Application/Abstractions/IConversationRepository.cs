using SharpChat.Domain.Conversations;

namespace SharpChat.Application.Abstractions;

public interface IConversationRepository
{
    Task<Conversation?> GetByIdAsync(Guid conversationId, CancellationToken cancellationToken);

    void Add(Conversation conversation);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
