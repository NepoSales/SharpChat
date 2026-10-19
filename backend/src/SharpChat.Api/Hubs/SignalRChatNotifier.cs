using Microsoft.AspNetCore.SignalR;
using SharpChat.Application.Abstractions;
using SharpChat.Application.Messages;

namespace SharpChat.Api.Hubs;

internal sealed class SignalRChatNotifier(IHubContext<ChatHub, IChatClient> hub) : IChatNotifier
{
    public Task MessageCreatedAsync(MessageDto message, IReadOnlyCollection<Guid> recipientIds, CancellationToken cancellationToken) =>
        hub.Clients.Users(recipientIds.Select(id => id.ToString())).MessageReceived(message);
}
