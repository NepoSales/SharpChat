using Microsoft.AspNetCore.Hosting;
using SharpChat.IntegrationTests.Conversations;
using SharpChat.IntegrationTests.Infrastructure;
using SharpChat.IntegrationTests.Messaging;

namespace SharpChat.IntegrationTests.Scale;

[Collection(ApiTestGroup.Name)]
public sealed class PresenceTests(SharpChatApiFactory factory)
{
    private static readonly TimeSpan QuietPeriod = TimeSpan.FromMilliseconds(500);

    [Fact]
    public async Task Contacts_see_a_user_come_online_and_go_offline_across_instances()
    {
        await using var instanceB = factory.WithWebHostBuilder(builder => builder.UseSetting("InstanceName", "instance-b"));
        var alice = await TestUser.CreateAsync(factory);
        var bob = await TestUser.CreateAsync(factory);
        await alice.CreateConversationOkAsync("team", bob);
        await using var aliceChat = await alice.ConnectToChatAsync(factory);

        var bobChat = await bob.ConnectToChatAsync(instanceB);
        var online = await aliceChat.NextPresenceAsync();
        await bobChat.DisposeAsync();
        var offline = await aliceChat.NextPresenceAsync();

        Assert.Equal((bob.Id, true), (online.UserId, online.IsOnline));
        Assert.Equal((bob.Id, false), (offline.UserId, offline.IsOnline));
    }

    [Fact]
    public async Task A_user_stays_online_while_any_tab_is_open()
    {
        var alice = await TestUser.CreateAsync(factory);
        var bob = await TestUser.CreateAsync(factory);
        await alice.CreateConversationOkAsync("team", bob);
        await using var aliceChat = await alice.ConnectToChatAsync(factory);
        var firstTab = await bob.ConnectToChatAsync(factory);
        await aliceChat.NextPresenceAsync();

        var secondTab = await bob.ConnectToChatAsync(factory);
        await aliceChat.AssertNoPresenceAsync(QuietPeriod);
        await firstTab.DisposeAsync();
        await aliceChat.AssertNoPresenceAsync(QuietPeriod);
        await secondTab.DisposeAsync();

        Assert.False((await aliceChat.NextPresenceAsync()).IsOnline);
    }

    [Fact]
    public async Task Tabs_closed_while_connecting_still_go_offline()
    {
        await using var freshInstance = factory.WithWebHostBuilder(builder => builder.UseSetting("InstanceName", "fresh"));
        var bob = await TestUser.CreateAsync(freshInstance);
        var firstTab = await bob.ConnectToChatAsync(freshInstance);
        var secondTab = await bob.ConnectToChatAsync(freshInstance);
        await firstTab.DisposeAsync();
        await secondTab.DisposeAsync();

        var alice = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("team", bob);
        await using var aliceChat = await alice.ConnectToChatAsync(factory);
        var deadline = DateTime.UtcNow.AddSeconds(5);
        IReadOnlyList<Guid> online;
        do
        {
            await Task.Delay(100);
            online = (await aliceChat.GetOnlineMembersAsync(conversation.Id)).Data!;
        }
        while (online.Contains(bob.Id) && DateTime.UtcNow < deadline);

        Assert.DoesNotContain(bob.Id, online);
    }

    [Fact]
    public async Task Strangers_are_not_told_about_presence()
    {
        var alice = await TestUser.CreateAsync(factory);
        var stranger = await TestUser.CreateAsync(factory);
        await using var strangerChat = await stranger.ConnectToChatAsync(factory);

        await using var aliceChat = await alice.ConnectToChatAsync(factory);

        await strangerChat.AssertNoPresenceAsync(QuietPeriod);
    }

    [Fact]
    public async Task Members_can_ask_who_is_online_in_a_conversation()
    {
        var alice = await TestUser.CreateAsync(factory);
        var bob = await TestUser.CreateAsync(factory);
        var carol = await TestUser.CreateAsync(factory);
        var outsider = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("team", bob, carol);
        await using var aliceChat = await alice.ConnectToChatAsync(factory);
        await using var bobChat = await bob.ConnectToChatAsync(factory);
        await using var outsiderChat = await outsider.ConnectToChatAsync(factory);

        var online = await aliceChat.GetOnlineMembersAsync(conversation.Id);
        var forOutsider = await outsiderChat.GetOnlineMembersAsync(conversation.Id);

        Assert.Equal(new[] { alice.Id, bob.Id }.Order(), online.Data!.Order());
        Assert.Equal("conversation_not_found", forOutsider.Error?.Code);
    }

    [Fact]
    public async Task Typing_reaches_the_other_members_only()
    {
        var alice = await TestUser.CreateAsync(factory);
        var bob = await TestUser.CreateAsync(factory);
        var outsider = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("team", bob);
        await using var aliceChat = await alice.ConnectToChatAsync(factory);
        await using var bobChat = await bob.ConnectToChatAsync(factory);
        await using var outsiderChat = await outsider.ConnectToChatAsync(factory);
        var parentId = Guid.NewGuid();

        await aliceChat.TypingAsync(conversation.Id, parentId);
        await outsiderChat.TypingAsync(conversation.Id);

        var typing = await bobChat.NextTypingAsync();
        Assert.Equal((conversation.Id, parentId, alice.Id, alice.UserName), (typing.ConversationId, typing.ParentMessageId, typing.UserId, typing.UserName));
        await aliceChat.AssertNoTypingAsync(QuietPeriod);
        await bobChat.AssertNoTypingAsync(QuietPeriod);
    }
}
