using Microsoft.AspNetCore.SignalR;
using SharpChat.Application.Abstractions;
using SharpChat.Application.Messages;

namespace SharpChat.Api.Hubs;

internal sealed class SignalRChatNotifier(IHubContext<ChatHub, IChatClient> hub) : IChatNotifier
{
    public Task MessageCreatedAsync(MessageDto message, IReadOnlyCollection<Guid> recipientIds, CancellationToken cancellationToken) =>
        Users(recipientIds).MessageReceived(message);

    public Task ReceiptUpdatedAsync(ReceiptDto receipt, IReadOnlyCollection<Guid> recipientIds, CancellationToken cancellationToken) =>
        Users(recipientIds).ReceiptUpdated(receipt);

    private IChatClient Users(IReadOnlyCollection<Guid> ids) => hub.Clients.Users(ids.Select(id => id.ToString()));
}
