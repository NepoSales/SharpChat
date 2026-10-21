using SharpChat.Application.Abstractions;
using SharpChat.Application.Diagnostics;
using SharpChat.Domain.Common;
using SharpChat.Domain.Conversations;

namespace SharpChat.Application.Presence;

public sealed class PresenceService(
    IPresenceTracker tracker,
    IConversationQueries conversations,
    IChatNotifier notifier,
    ChatMetrics metrics)
{
    public async Task ConnectedAsync(Guid userId, string connectionId, CancellationToken cancellationToken)
    {
        if (await tracker.ConnectAsync(userId, connectionId, cancellationToken))
        {
            metrics.PresenceChanged(isOnline: true);
            await NotifyContactsAsync(new PresenceDto(userId, IsOnline: true), cancellationToken);
        }
    }

    public async Task DisconnectedAsync(Guid userId, string connectionId, CancellationToken cancellationToken)
    {
        if (await tracker.DisconnectAsync(userId, connectionId, cancellationToken))
        {
            metrics.PresenceChanged(isOnline: false);
            await NotifyContactsAsync(new PresenceDto(userId, IsOnline: false), cancellationToken);
        }
    }

    public async Task<Result<IReadOnlyList<Guid>>> GetOnlineMembersAsync(
        Guid userId, Guid conversationId, CancellationToken cancellationToken)
    {
        var members = await conversations.ListMembersAsync(conversationId, cancellationToken);
        if (members.All(m => m.UserId != userId))
        {
            return ConversationErrors.NotFound;
        }

        var online = await tracker.GetOnlineAsync(members.Select(m => m.UserId).ToList(), cancellationToken);
        return online.ToList();
    }

    private async Task NotifyContactsAsync(PresenceDto presence, CancellationToken cancellationToken)
    {
        var contacts = await conversations.ListContactIdsAsync(presence.UserId, cancellationToken);
        if (contacts.Count > 0)
        {
            await notifier.PresenceChangedAsync(presence, contacts, cancellationToken);
        }
    }
}
