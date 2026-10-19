using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using SharpChat.Application.Conversations;
using SharpChat.Application.Messages;
using SharpChat.IntegrationTests.Auth;
using SharpChat.IntegrationTests.Conversations;
using SharpChat.IntegrationTests.Infrastructure;

namespace SharpChat.IntegrationTests.Messaging;

[Collection(ApiTestGroup.Name)]
public sealed class MessageHistoryTests(SharpChatApiFactory factory)
{
    [Fact]
    public async Task Pages_cover_every_message_once_even_with_equal_timestamps()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var frozen = factory.WithWebHostBuilder(b =>
            b.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(clock)));
        var alice = await TestUser.CreateAsync(frozen);
        var conversation = await alice.CreateConversationOkAsync("team");
        await using var chat = await alice.ConnectToChatAsync(frozen);

        var sent = new List<Guid>();
        for (var i = 0; i < 7; i++)
        {
            sent.Add((await chat.SendOkAsync(conversation.Id, $"message {i}")).Id);
            if (i == 3)
            {
                clock.Advance(TimeSpan.FromSeconds(1));
            }
        }

        var pages = new List<MessagePage>();
        string? cursor = null;
        do
        {
            var page = await alice.GetHistoryAsync(conversation.Id, cursor, limit: 3);
            pages.Add(page);
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        Assert.Equal([3, 3, 1], pages.Select(p => p.Items.Count));
        var all = pages.AsEnumerable().Reverse().SelectMany(p => p.Items).ToList();
        Assert.Equal(sent.Order(), all.Select(m => m.Id).Order());
        Assert.Equal(all.OrderBy(m => m.SentAt).ThenBy(m => m.Id.ToString()).Select(m => m.SentAt), all.Select(m => m.SentAt));
    }

    [Fact]
    public async Task Newest_page_comes_first_in_chronological_order()
    {
        var alice = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("team");
        await using var chat = await alice.ConnectToChatAsync(factory);
        foreach (var text in new[] { "one", "two", "three" })
        {
            await chat.SendOkAsync(conversation.Id, text);
        }

        var page = await alice.GetHistoryAsync(conversation.Id, limit: 2);

        Assert.Equal(["two", "three"], page.Items.Select(m => m.Content));
        Assert.NotNull(page.NextCursor);
        var older = await alice.GetHistoryAsync(conversation.Id, page.NextCursor, limit: 2);
        Assert.Equal(["one"], older.Items.Select(m => m.Content));
        Assert.Null(older.NextCursor);
    }

    [Fact]
    public async Task Non_members_cannot_read_history()
    {
        var alice = await TestUser.CreateAsync(factory);
        var outsider = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("private");

        var response = await outsider.Client.GetAsync($"/conversations/{conversation.Id}/messages");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_cursor_is_rejected()
    {
        var alice = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("team");

        var response = await alice.Client.GetAsync($"/conversations/{conversation.Id}/messages?before=garbage");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Conversations_are_listed_by_latest_activity()
    {
        var alice = await TestUser.CreateAsync(factory);
        var older = await alice.CreateConversationOkAsync("older");
        var newer = await alice.CreateConversationOkAsync("newer");
        await using var chat = await alice.ConnectToChatAsync(factory);

        var sent = await chat.SendOkAsync(older.Id, "bump");

        var list = (await alice.Client.GetFromJsonAsync<List<ConversationSummaryDto>>("/conversations", TestUser.Json))!;
        Assert.Equal([older.Id, newer.Id], list.Select(c => c.Id));
        Assert.Equal(sent.SentAt, list[0].LastMessageAt);
        Assert.Null(list[1].LastMessageAt);
    }
}

internal static class MessageHistoryApi
{
    public static async Task<MessagePage> GetHistoryAsync(this TestUser user, Guid conversationId, string? before = null, int? limit = null)
    {
        var query = new List<string>();
        if (before is not null)
        {
            query.Add($"before={Uri.EscapeDataString(before)}");
        }

        if (limit is not null)
        {
            query.Add($"limit={limit}");
        }

        var url = $"/conversations/{conversationId}/messages{(query.Count > 0 ? "?" + string.Join('&', query) : string.Empty)}";
        var response = await user.Client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.ReadAsAsync<MessagePage>();
    }
}
