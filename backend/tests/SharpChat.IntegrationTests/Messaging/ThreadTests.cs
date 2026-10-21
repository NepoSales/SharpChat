using System.Net;
using System.Net.Http.Json;
using SharpChat.Application.Conversations;
using SharpChat.Application.Messages;
using SharpChat.IntegrationTests.Conversations;
using SharpChat.IntegrationTests.Infrastructure;

namespace SharpChat.IntegrationTests.Messaging;

[Collection(ApiTestGroup.Name)]
public sealed class ThreadTests(SharpChatApiFactory factory)
{
    [Fact]
    public async Task Replies_reach_members_and_update_the_parent()
    {
        var alice = await TestUser.CreateAsync(factory);
        var bob = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("team", bob);
        await using var aliceChat = await alice.ConnectToChatAsync(factory);
        await using var bobChat = await bob.ConnectToChatAsync(factory);
        var parent = await aliceChat.SendOkAsync(conversation.Id, "deploy at 5?");
        await aliceChat.NextMessageAsync();
        await bobChat.NextMessageAsync();

        var reply = await bobChat.ReplyOkAsync(conversation.Id, parent.Id, "works for me");

        Assert.Equal(parent.Id, reply.ParentMessageId);
        Assert.Equal(reply.Id, (await aliceChat.NextMessageAsync()).Id);
        var updatedParent = await aliceChat.NextUpdateAsync();
        Assert.Equal(parent.Id, updatedParent.Id);
        Assert.Equal((1, reply.Seq, reply.SentAt), (updatedParent.ReplyCount, updatedParent.LastReplySeq, updatedParent.LastReplyAt));
        Assert.Equal(reply.Seq + 1, updatedParent.ChangeSeq);
        Assert.Equal(parent.Id, (await bobChat.NextUpdateAsync()).Id);
    }

    [Fact]
    public async Task Replies_stay_out_of_the_main_history_and_have_their_own()
    {
        var alice = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("team");
        await using var chat = await alice.ConnectToChatAsync(factory);
        var parent = await chat.SendOkAsync(conversation.Id, "question");
        await chat.ReplyOkAsync(conversation.Id, parent.Id, "answer 1");
        await chat.ReplyOkAsync(conversation.Id, parent.Id, "answer 2");
        await chat.SendOkAsync(conversation.Id, "next topic");

        var history = await alice.Client.GetFromJsonAsync<MessagePage>($"/conversations/{conversation.Id}/messages", TestUser.Json);
        var replies = await alice.Client.GetFromJsonAsync<MessagePage>(
            $"/conversations/{conversation.Id}/messages/{parent.Id}/replies", TestUser.Json);
        var changes = await alice.Client.GetFromJsonAsync<MessageChanges>(
            $"/conversations/{conversation.Id}/changes?after=0", TestUser.Json);

        Assert.Equal(["question", "next topic"], history!.Items.Select(m => m.Content));
        Assert.Equal(2, history.Items[0].ReplyCount);
        Assert.Equal(["answer 1", "answer 2"], replies!.Items.Select(m => m.Content));
        Assert.Equal(
            ["answer 1", "answer 2", "question", "next topic"],
            changes!.Items.Select(m => m.Content));
    }

    [Fact]
    public async Task Replies_page_with_the_same_cursor_as_history()
    {
        var alice = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("team");
        await using var chat = await alice.ConnectToChatAsync(factory);
        var parent = await chat.SendOkAsync(conversation.Id, "thread");
        for (var i = 1; i <= 5; i++)
        {
            await chat.ReplyOkAsync(conversation.Id, parent.Id, $"r{i}");
        }

        var url = $"/conversations/{conversation.Id}/messages/{parent.Id}/replies?limit=3";
        var newest = await alice.Client.GetFromJsonAsync<MessagePage>(url, TestUser.Json);
        var older = await alice.Client.GetFromJsonAsync<MessagePage>(
            $"{url}&before={Uri.EscapeDataString(newest!.NextCursor!)}", TestUser.Json);

        Assert.Equal(["r3", "r4", "r5"], newest.Items.Select(m => m.Content));
        Assert.Equal(["r1", "r2"], older!.Items.Select(m => m.Content));
        Assert.Null(older.NextCursor);
    }

    [Fact]
    public async Task Threads_are_one_level_deep_and_stay_in_their_conversation()
    {
        var alice = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("team");
        var other = await alice.CreateConversationOkAsync("other");
        await using var chat = await alice.ConnectToChatAsync(factory);
        var parent = await chat.SendOkAsync(conversation.Id, "root");
        var reply = await chat.ReplyOkAsync(conversation.Id, parent.Id, "reply");

        var nested = await chat.ReplyAsync(conversation.Id, reply.Id, "reply to reply");
        var crossConversation = await chat.ReplyAsync(other.Id, parent.Id, "wrong place");
        var unknown = await chat.ReplyAsync(conversation.Id, Guid.NewGuid(), "nobody");
        var repliesOfReply = await alice.Client.GetAsync($"/conversations/{conversation.Id}/messages/{reply.Id}/replies");

        Assert.Equal("nested_reply", nested.Error?.Code);
        Assert.Equal("parent_message_not_found", crossConversation.Error?.Code);
        Assert.Equal("parent_message_not_found", unknown.Error?.Code);
        Assert.Equal(HttpStatusCode.NotFound, repliesOfReply.StatusCode);
    }

    [Fact]
    public async Task A_single_message_can_be_loaded_by_members_only()
    {
        var alice = await TestUser.CreateAsync(factory);
        var outsider = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("team");
        var other = await alice.CreateConversationOkAsync("other");
        await using var chat = await alice.ConnectToChatAsync(factory);
        var message = await chat.SendOkAsync(conversation.Id, "link me");

        var loaded = await alice.Client.GetFromJsonAsync<MessageDto>(
            $"/conversations/{conversation.Id}/messages/{message.Id}", TestUser.Json);
        var wrongConversation = await alice.Client.GetAsync($"/conversations/{other.Id}/messages/{message.Id}");
        var byOutsider = await outsider.Client.GetAsync($"/conversations/{conversation.Id}/messages/{message.Id}");

        Assert.Equal("link me", loaded!.Content);
        Assert.Equal(HttpStatusCode.NotFound, wrongConversation.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, byOutsider.StatusCode);
    }

    [Fact]
    public async Task Non_members_cannot_read_a_thread()
    {
        var alice = await TestUser.CreateAsync(factory);
        var outsider = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("private");
        await using var chat = await alice.ConnectToChatAsync(factory);
        var parent = await chat.SendOkAsync(conversation.Id, "secret");

        var response = await outsider.Client.GetAsync($"/conversations/{conversation.Id}/messages/{parent.Id}/replies");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Replies_do_not_count_as_unread()
    {
        var alice = await TestUser.CreateAsync(factory);
        var bob = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("team", bob);
        await using var aliceChat = await alice.ConnectToChatAsync(factory);
        var parent = await aliceChat.SendOkAsync(conversation.Id, "root");
        await aliceChat.ReplyOkAsync(conversation.Id, parent.Id, "r1");
        await aliceChat.ReplyOkAsync(conversation.Id, parent.Id, "r2");

        var list = await bob.Client.GetFromJsonAsync<List<ConversationSummaryDto>>("/conversations", TestUser.Json);

        var summary = list!.Single(c => c.Id == conversation.Id);
        Assert.Equal(1, summary.UnreadCount);
        Assert.Equal(4, summary.LastSeq);
    }
}
