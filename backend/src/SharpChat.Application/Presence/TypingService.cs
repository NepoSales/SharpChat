using SharpChat.Application.Abstractions;
using SharpChat.Domain.Common;
using SharpChat.Domain.Conversations;

namespace SharpChat.Application.Presence;

public sealed class TypingService(IConversationQueries conversations, IChatNotifier notifier)
{
    public async Task<Result> TypingAsync(Guid userId, TypingRequest request, CancellationToken cancellationToken)
    {
        var members = await conversations.ListMembersAsync(request.ConversationId, cancellationToken);
        var typist = members.FirstOrDefault(m => m.UserId == userId);
        if (typist is null)
        {
            return ConversationErrors.NotFound;
        }

        var others = members.Where(m => m.UserId != userId).Select(m => m.UserId).ToList();
        if (others.Count > 0)
        {
            await notifier.UserTypingAsync(
                new TypingDto(request.ConversationId, request.ParentMessageId, userId, typist.UserName),
                others,
                cancellationToken);
        }

        return Result.Success();
    }
}
