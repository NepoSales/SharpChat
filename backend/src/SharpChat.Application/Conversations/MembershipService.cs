using SharpChat.Application.Abstractions;
using SharpChat.Domain.Conversations;

namespace SharpChat.Application.Conversations;

public sealed class MembershipService(IConversationQueries queries)
{
    public Task<ConversationRole?> GetRoleAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken) =>
        queries.GetRoleAsync(conversationId, userId, cancellationToken);

    public async Task<bool> IsMemberAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken) =>
        await GetRoleAsync(conversationId, userId, cancellationToken) is not null;
}
