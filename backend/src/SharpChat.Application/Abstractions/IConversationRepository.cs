using SharpChat.Domain.Conversations;

namespace SharpChat.Application.Abstractions;

/// <summary>Loads and saves the <see cref="Conversation"/> aggregate (with its members) for commands.</summary>
public interface IConversationRepository
{
    Task<Conversation?> GetByIdAsync(Guid conversationId, CancellationToken cancellationToken);

    void Add(Conversation conversation);

    /// <exception cref="Common.ConcurrencyConflictException">The conversation was changed by another request.</exception>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
