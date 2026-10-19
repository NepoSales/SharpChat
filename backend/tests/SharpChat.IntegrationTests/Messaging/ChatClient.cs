using System.Text.Json.Serialization;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using SharpChat.Api.Hubs;
using SharpChat.Application.Messages;
using SharpChat.IntegrationTests.Infrastructure;

namespace SharpChat.IntegrationTests.Messaging;

public sealed class ChatClient : IAsyncDisposable
{
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(5);

    private readonly HubConnection _connection;
    private readonly Channel<MessageDto> _received = Channel.CreateUnbounded<MessageDto>();

    private ChatClient(HubConnection connection)
    {
        _connection = connection;
        _connection.On<MessageDto>(nameof(IChatClient.MessageReceived), message => _received.Writer.TryWrite(message));
    }

    public static async Task<ChatClient> ConnectAsync<T>(WebApplicationFactory<T> factory, string? accessToken)
        where T : class
    {
        var query = accessToken is null ? string.Empty : $"?access_token={Uri.EscapeDataString(accessToken)}";
        var url = new Uri(factory.Server.BaseAddress, $"{ChatHub.Path.TrimStart('/')}{query}");

        var connection = new HubConnectionBuilder()
            .WithUrl(url, options =>
            {
                options.Transports = HttpTransportType.WebSockets;
                options.SkipNegotiation = true;
                options.WebSocketFactory = async (context, cancellationToken) =>
                    await factory.Server.CreateWebSocketClient().ConnectAsync(context.Uri, cancellationToken);
            })
            .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .Build();

        await connection.StartAsync();
        return new ChatClient(connection);
    }

    public Task<HubResponse<MessageDto>> SendAsync(Guid conversationId, string content, Guid? messageId = null) =>
        _connection.InvokeAsync<HubResponse<MessageDto>>(
            nameof(ChatHub.SendMessage), new SendMessageRequest(conversationId, messageId ?? Guid.NewGuid(), content));

    public async Task<MessageDto> SendOkAsync(Guid conversationId, string content)
    {
        var response = await SendAsync(conversationId, content);
        Assert.Null(response.Error);
        return response.Data!;
    }

    public async Task<MessageDto> NextMessageAsync()
    {
        using var timeout = new CancellationTokenSource(ReceiveTimeout);
        return await _received.Reader.ReadAsync(timeout.Token);
    }

    public async Task AssertNothingReceivedAsync(TimeSpan wait)
    {
        await Task.Delay(wait);
        Assert.False(_received.Reader.TryRead(out var unexpected), $"Unexpected message: {unexpected?.Content}");
    }

    public ValueTask DisposeAsync() => _connection.DisposeAsync();
}

internal static class ChatClientExtensions
{
    public static Task<ChatClient> ConnectToChatAsync<T>(this TestUser user, WebApplicationFactory<T> factory)
        where T : class =>
        ChatClient.ConnectAsync(factory, user.AccessToken);
}
