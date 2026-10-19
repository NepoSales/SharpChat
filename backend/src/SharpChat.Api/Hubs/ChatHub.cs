using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SharpChat.Application.Messages;

namespace SharpChat.Api.Hubs;

public interface IChatClient
{
    Task MessageReceived(MessageDto message);
}

[Authorize]
public sealed class ChatHub(MessageService messages) : Hub<IChatClient>
{
    public const string Path = "/hubs/chat";

    public async Task<HubResponse<MessageDto>> SendMessage(SendMessageRequest request)
    {
        var senderId = Guid.Parse(Context.UserIdentifier!);

        var result = await messages.SendAsync(senderId, request, Context.ConnectionAborted);
        return result.IsSuccess ? HubResponse<MessageDto>.Ok(result.Value) : HubResponse<MessageDto>.Fail(result.Error);
    }
}
