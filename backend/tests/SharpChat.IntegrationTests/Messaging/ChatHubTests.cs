using SharpChat.IntegrationTests.Conversations;
using SharpChat.IntegrationTests.Infrastructure;

namespace SharpChat.IntegrationTests.Messaging;

[Collection(ApiTestGroup.Name)]
public sealed class ChatHubTests(SharpChatApiFactory factory)
{
    private static readonly TimeSpan QuietPeriod = TimeSpan.FromMilliseconds(500);

    [Fact]
    public async Task Members_receive_messages_in_real_time_and_the_sender_gets_an_ack()
    {
        var alice = await TestUser.CreateAsync(factory);
        var bob = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("team", bob);
        await using var aliceChat = await alice.ConnectToChatAsync(factory);
        await using var bobChat = await bob.ConnectToChatAsync(factory);

        var ack = await aliceChat.SendAsync(conversation.Id, "hello **bob**\n```csharp\nvar x = 1;\n```");

        Assert.Null(ack.Error);
        var received = await bobChat.NextMessageAsync();
        Assert.Equal(ack.Data!.Id, received.Id);
        Assert.Equal("hello **bob**\n```csharp\nvar x = 1;\n```", received.Content);
        Assert.Equal(alice.Id, received.SenderId);
        Assert.Equal(alice.UserName, received.SenderUserName);
        Assert.Equal(ack.Data.SentAt, received.SentAt);
        Assert.Equal(ack.Data.Id, (await aliceChat.NextMessageAsync()).Id);
    }

    [Fact]
    public async Task Non_members_cannot_send_and_do_not_receive()
    {
        var alice = await TestUser.CreateAsync(factory);
        var outsider = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("private");
        await using var aliceChat = await alice.ConnectToChatAsync(factory);
        await using var outsiderChat = await outsider.ConnectToChatAsync(factory);

        var attempt = await outsiderChat.SendAsync(conversation.Id, "let me in");
        await aliceChat.SendOkAsync(conversation.Id, "secret");

        Assert.Equal("conversation_not_found", attempt.Error?.Code);
        await outsiderChat.AssertNothingReceivedAsync(QuietPeriod);
    }

    [Fact]
    public async Task Connection_without_a_valid_token_is_rejected()
    {
        await Assert.ThrowsAnyAsync<Exception>(() => ChatClient.ConnectAsync(factory, accessToken: null));
        await Assert.ThrowsAnyAsync<Exception>(() => ChatClient.ConnectAsync(factory, "not-a-jwt"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Empty_messages_are_rejected(string content)
    {
        var alice = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("team");
        await using var chat = await alice.ConnectToChatAsync(factory);

        var response = await chat.SendAsync(conversation.Id, content);

        Assert.Equal("validation_failed", response.Error?.Code);
        Assert.True(response.Error!.Errors!.ContainsKey("Content"));
    }

    [Fact]
    public async Task Messages_over_the_length_limit_are_rejected()
    {
        var alice = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("team");
        await using var chat = await alice.ConnectToChatAsync(factory);

        var response = await chat.SendAsync(conversation.Id, new string('a', 4001));

        Assert.Equal("validation_failed", response.Error?.Code);
    }

    [Fact]
    public async Task Resending_the_same_message_id_stores_it_once()
    {
        var alice = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("team");
        await using var chat = await alice.ConnectToChatAsync(factory);
        var messageId = Guid.NewGuid();

        var first = await chat.SendAsync(conversation.Id, "once", messageId);
        var retry = await chat.SendAsync(conversation.Id, "once", messageId);

        Assert.Equal(first.Data, retry.Data);
        var history = await alice.GetHistoryAsync(conversation.Id);
        Assert.Equal([messageId], history.Items.Select(m => m.Id));
    }

    [Fact]
    public async Task Message_id_of_another_users_message_cannot_be_reused()
    {
        var alice = await TestUser.CreateAsync(factory);
        var bob = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("team", bob);
        await using var aliceChat = await alice.ConnectToChatAsync(factory);
        await using var bobChat = await bob.ConnectToChatAsync(factory);
        var messageId = Guid.NewGuid();

        await aliceChat.SendAsync(conversation.Id, "mine", messageId);
        var hijack = await bobChat.SendAsync(conversation.Id, "also mine?", messageId);

        Assert.Equal("message_id_conflict", hijack.Error?.Code);
    }

    [Fact]
    public async Task Removed_members_stop_receiving_and_new_members_start_right_away()
    {
        var alice = await TestUser.CreateAsync(factory);
        var bob = await TestUser.CreateAsync(factory);
        var carol = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("team", bob);
        await using var aliceChat = await alice.ConnectToChatAsync(factory);
        await using var bobChat = await bob.ConnectToChatAsync(factory);
        await using var carolChat = await carol.ConnectToChatAsync(factory);

        await alice.RemoveMemberAsync(conversation.Id, bob.Id);
        await alice.AddMemberAsync(conversation.Id, carol.UserName);
        await aliceChat.SendOkAsync(conversation.Id, "after the change");

        Assert.Equal("after the change", (await carolChat.NextMessageAsync()).Content);
        await bobChat.AssertNothingReceivedAsync(QuietPeriod);
    }

    [Fact]
    public async Task Every_connection_of_a_user_receives_the_message()
    {
        var alice = await TestUser.CreateAsync(factory);
        var bob = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("team", bob);
        await using var aliceChat = await alice.ConnectToChatAsync(factory);
        await using var bobLaptop = await bob.ConnectToChatAsync(factory);
        await using var bobPhone = await bob.ConnectToChatAsync(factory);

        var sent = await aliceChat.SendOkAsync(conversation.Id, "hi");

        Assert.Equal(sent.Id, (await bobLaptop.NextMessageAsync()).Id);
        Assert.Equal(sent.Id, (await bobPhone.NextMessageAsync()).Id);
    }
}
