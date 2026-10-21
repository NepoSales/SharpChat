using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SharpChat.Application.Messages;
using SharpChat.Domain.Conversations;

namespace SharpChat.Api.Hubs;

public interface IChatClient
{
    Task MessageReceived(MessageDto message);

    Task ReceiptUpdated(ReceiptDto receipt);
}

[Authorize]
public sealed class ChatHub(MessageService messages, ReceiptService receipts) : Hub<IChatClient>
{
    public const string Path = "/hubs/chat";

    private Guid UserId => Guid.Parse(Context.UserIdentifier!);

    public async Task<HubResponse<MessageDto>> SendMessage(SendMessageRequest request)
    {
        var result = await messages.SendAsync(UserId, request, Context.ConnectionAborted);
        return result.IsSuccess ? HubResponse<MessageDto>.Ok(result.Value) : HubResponse<MessageDto>.Fail(result.Error);
    }

    public Task<HubResponse<ReceiptDto>> MarkDelivered(MarkReceiptRequest request) => Mark(request, ReceiptKind.Delivered);

    public Task<HubResponse<ReceiptDto>> MarkRead(MarkReceiptRequest request) => Mark(request, ReceiptKind.Read);

    private async Task<HubResponse<ReceiptDto>> Mark(MarkReceiptRequest request, ReceiptKind kind)
    {
        var result = await receipts.MarkAsync(UserId, request, kind, Context.ConnectionAborted);
        return result.IsSuccess ? HubResponse<ReceiptDto>.Ok(result.Value) : HubResponse<ReceiptDto>.Fail(result.Error);
    }
}
