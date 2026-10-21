using Microsoft.AspNetCore.SignalR;
using SharpChat.Application.Abstractions;
using SharpChat.Application.Messages;
using SharpChat.Application.Presence;

namespace SharpChat.Api.Hubs;

internal sealed class SignalRChatNotifier(IHubContext<ChatHub, IChatClient> hub) : IChatNotifier
{
    public Task MessageCreatedAsync(MessageDto message, IReadOnlyCollection<Guid> recipientIds, CancellationToken cancellationToken) =>
        Users(recipientIds).MessageReceived(message);

    public Task MessageUpdatedAsync(MessageDto message, IReadOnlyCollection<Guid> recipientIds, CancellationToken cancellationToken) =>
        Users(recipientIds).MessageUpdated(message);

    public Task ReceiptUpdatedAsync(ReceiptDto receipt, IReadOnlyCollection<Guid> recipientIds, CancellationToken cancellationToken) =>
        Users(recipientIds).ReceiptUpdated(receipt);

    public Task PresenceChangedAsync(PresenceDto presence, IReadOnlyCollection<Guid> recipientIds, CancellationToken cancellationToken) =>
        Users(recipientIds).PresenceChanged(presence);

    public Task UserTypingAsync(TypingDto typing, IReadOnlyCollection<Guid> recipientIds, CancellationToken cancellationToken) =>
        Users(recipientIds).UserTyping(typing);

    private IChatClient Users(IReadOnlyCollection<Guid> ids) => hub.Clients.Users(ids.Select(id => id.ToString()));
}
