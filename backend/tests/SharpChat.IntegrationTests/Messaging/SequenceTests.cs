using System.Net;
using SharpChat.Application.Messages;
using SharpChat.IntegrationTests.Conversations;
using SharpChat.IntegrationTests.Infrastructure;

namespace SharpChat.IntegrationTests.Messaging;

[Collection(ApiTestGroup.Name)]
public sealed class SequenceTests(SharpChatApiFactory factory)
{
    [Fact]
    public async Task Each_conversation_numbers_its_messages_from_one()
    {
        var alice = await TestUser.CreateAsync(factory);
        var first = await alice.CreateConversationOkAsync("first");
        var second = await alice.CreateConversationOkAsync("second");
        await using var chat = await alice.ConnectToChatAsync(factory);

        var inFirst = new List<long>();
        for (var i = 0; i < 3; i++)
        {
            inFirst.Add((await chat.SendOkAsync(first.Id, $"a{i}")).Seq);
        }

        var inSecond = (await chat.SendOkAsync(second.Id, "b")).Seq;

        Assert.Equal([1, 2, 3], inFirst);
        Assert.Equal(1, inSecond);
    }

    [Fact]
    public async Task Concurrent_senders_get_unique_consecutive_numbers()
    {
        var alice = await TestUser.CreateAsync(factory);
        var bob = await TestUser.CreateAsync(factory);
        var carol = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("busy", bob, carol);
        await using var aliceChat = await alice.ConnectToChatAsync(factory);
        await using var bobChat = await bob.ConnectToChatAsync(factory);
        await using var carolChat = await carol.ConnectToChatAsync(factory);

        var sends = new[] { aliceChat, bobChat, carolChat }
            .SelectMany(chat => Enumerable.Range(0, 10).Select(i => chat.SendOkAsync(conversation.Id, $"m{i}")));
        var acks = await Task.WhenAll(sends);

        Assert.Equal(Enumerable.Range(1, 30).Select(i => (long)i), acks.Select(a => a.Seq).Order());
        Assert.All(acks, ack => Assert.Equal(ack.Seq, ack.ChangeSeq));
    }

    [Fact]
    public async Task Simultaneous_resends_of_the_same_id_store_the_message_once()
    {
        var alice = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("retry");
        await using var chat = await alice.ConnectToChatAsync(factory);
        await using var otherTab = await alice.ConnectToChatAsync(factory);
        var messageId = Guid.NewGuid();

        var acks = await Task.WhenAll(Enumerable.Range(0, 6).Select(i =>
            (i % 2 == 0 ? chat : otherTab).SendAsync(conversation.Id, "only once", messageId)));

        Assert.All(acks, ack => Assert.Null(ack.Error));
        Assert.Single(acks.Select(a => (a.Data!.Id, a.Data.Seq, a.Data.SentAt)).Distinct());
        var history = await alice.Client.GetAsync($"/conversations/{conversation.Id}/messages");
        Assert.Single((await history.ReadAsAsync<MessagePage>()).Items);
    }

    [Fact]
    public async Task Changes_return_everything_after_a_position_in_order()
    {
        var alice = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("sync");
        await using var chat = await alice.ConnectToChatAsync(factory);
        for (var i = 1; i <= 5; i++)
        {
            await chat.SendOkAsync(conversation.Id, $"m{i}");
        }

        var all = await alice.Client.GetAsync($"/conversations/{conversation.Id}/changes?after=2");
        var page = await alice.Client.GetAsync($"/conversations/{conversation.Id}/changes?after=0&limit=2");
        var none = await alice.Client.GetAsync($"/conversations/{conversation.Id}/changes?after=5");

        var rest = await all.ReadAsAsync<MessageChanges>();
        Assert.Equal(["m3", "m4", "m5"], rest.Items.Select(m => m.Content));
        Assert.False(rest.HasMore);
        var firstPage = await page.ReadAsAsync<MessageChanges>();
        Assert.Equal([1, 2], firstPage.Items.Select(m => m.ChangeSeq));
        Assert.True(firstPage.HasMore);
        Assert.Empty((await none.ReadAsAsync<MessageChanges>()).Items);
    }

    [Fact]
    public async Task Changes_require_membership_and_a_valid_position()
    {
        var alice = await TestUser.CreateAsync(factory);
        var outsider = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("private");

        var forOutsider = await outsider.Client.GetAsync($"/conversations/{conversation.Id}/changes?after=0");
        var negative = await alice.Client.GetAsync($"/conversations/{conversation.Id}/changes?after=-1");

        Assert.Equal(HttpStatusCode.NotFound, forOutsider.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, negative.StatusCode);
    }
}
