using SharpChat.Application.Abstractions;
using SharpChat.Domain.Conversations;

namespace SharpChat.Application.Conversations;

/// <summary>
/// Answers "is this user a member of this conversation?". Used by every conversation endpoint and, later, the chat hub.
/// </summary>
public sealed class MembershipService(IConversationQueries queries)
{
    public Task<ConversationRole?> GetRoleAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken) =>
        queries.GetRoleAsync(conversationId, userId, cancellationToken);

    public async Task<bool> IsMemberAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken) =>
        await GetRoleAsync(conversationId, userId, cancellationToken) is not null;
}
