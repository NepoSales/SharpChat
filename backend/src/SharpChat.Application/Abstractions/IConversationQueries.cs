using SharpChat.Application.Conversations;
using SharpChat.Domain.Conversations;

namespace SharpChat.Application.Abstractions;

public interface IConversationQueries
{
    Task<IReadOnlyList<ConversationSummaryDto>> ListForUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<ConversationDto?> GetDetailsAsync(Guid conversationId, CancellationToken cancellationToken);

    Task<MemberDto?> GetMemberAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<MemberDto>> ListMembersAsync(Guid conversationId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> ListContactIdsAsync(Guid userId, CancellationToken cancellationToken);

    Task<ConversationRole?> GetRoleAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken);
}
