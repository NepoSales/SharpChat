using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SharpChat.Application.Messages;
using SharpChat.Application.Presence;
using SharpChat.Domain.Conversations;

namespace SharpChat.Api.Hubs;

public interface IChatClient
{
    Task MessageReceived(MessageDto message);

    Task MessageUpdated(MessageDto message);

    Task ReceiptUpdated(ReceiptDto receipt);

    Task PresenceChanged(PresenceDto presence);

    Task UserTyping(TypingDto typing);
}

[Authorize]
public sealed class ChatHub(
    MessageService messages,
    ReceiptService receipts,
    PresenceService presence,
    TypingService typing,
    ConnectionRegistry registry) : Hub<IChatClient>
{
    public const string Path = "/hubs/chat";

    private Guid UserId => Guid.Parse(Context.UserIdentifier!);

    public override async Task OnConnectedAsync()
    {
        registry.Add(Context.ConnectionId, UserId);
        try
        {
            await presence.ConnectedAsync(UserId, Context.ConnectionId, CancellationToken.None);
        }
        catch
        {
            registry.Remove(Context.ConnectionId);
            throw;
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        registry.Remove(Context.ConnectionId);
        await presence.DisconnectedAsync(UserId, Context.ConnectionId, CancellationToken.None);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task<HubResponse<MessageDto>> SendMessage(SendMessageRequest request)
    {
        var result = await messages.SendAsync(UserId, request, Context.ConnectionAborted);
        return result.IsSuccess ? HubResponse<MessageDto>.Ok(result.Value) : HubResponse<MessageDto>.Fail(result.Error);
    }

    public async Task<HubResponse<MessageDto>> EditMessage(EditMessageRequest request)
    {
        var result = await messages.EditAsync(UserId, request, Context.ConnectionAborted);
        return result.IsSuccess ? HubResponse<MessageDto>.Ok(result.Value) : HubResponse<MessageDto>.Fail(result.Error);
    }

    public async Task<HubResponse<MessageDto>> DeleteMessage(DeleteMessageRequest request)
    {
        var result = await messages.DeleteAsync(UserId, request, Context.ConnectionAborted);
        return result.IsSuccess ? HubResponse<MessageDto>.Ok(result.Value) : HubResponse<MessageDto>.Fail(result.Error);
    }

    public Task<HubResponse<ReceiptDto>> MarkDelivered(MarkReceiptRequest request) => Mark(request, ReceiptKind.Delivered);

    public Task<HubResponse<ReceiptDto>> MarkRead(MarkReceiptRequest request) => Mark(request, ReceiptKind.Read);

    public async Task<HubResponse<IReadOnlyList<Guid>>> GetOnlineMembers(Guid conversationId)
    {
        var result = await presence.GetOnlineMembersAsync(UserId, conversationId, Context.ConnectionAborted);
        return result.IsSuccess
            ? HubResponse<IReadOnlyList<Guid>>.Ok(result.Value)
            : HubResponse<IReadOnlyList<Guid>>.Fail(result.Error);
    }

    public Task Typing(TypingRequest request) => typing.TypingAsync(UserId, request, Context.ConnectionAborted);

    private async Task<HubResponse<ReceiptDto>> Mark(MarkReceiptRequest request, ReceiptKind kind)
    {
        var result = await receipts.MarkAsync(UserId, request, kind, Context.ConnectionAborted);
        return result.IsSuccess ? HubResponse<ReceiptDto>.Ok(result.Value) : HubResponse<ReceiptDto>.Fail(result.Error);
    }
}
