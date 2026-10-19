using SharpChat.Application.Conversations;
using SharpChat.Domain.Conversations;

namespace SharpChat.Application.Abstractions;

/// <summary>Read side for conversations: returns DTOs (with usernames) straight from the database.</summary>
public interface IConversationQueries
{
    Task<IReadOnlyList<ConversationSummaryDto>> ListForUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<ConversationDto?> GetDetailsAsync(Guid conversationId, CancellationToken cancellationToken);

    Task<MemberDto?> GetMemberAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken);

    Task<ConversationRole?> GetRoleAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken);
}
