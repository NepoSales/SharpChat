using System.Net.Http.Json;
using SharpChat.Application.Messages;
using SharpChat.Domain.Conversations;
using SharpChat.IntegrationTests.Conversations;
using SharpChat.IntegrationTests.Infrastructure;

namespace SharpChat.IntegrationTests.Messaging;

[Collection(ApiTestGroup.Name)]
public sealed class EditDeleteTests(SharpChatApiFactory factory)
{
    private static readonly TimeSpan QuietPeriod = TimeSpan.FromMilliseconds(500);

    [Fact]
    public async Task The_author_edits_and_members_receive_the_new_version()
    {
        var alice = await TestUser.CreateAsync(factory);
        var bob = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("team", bob);
        await using var aliceChat = await alice.ConnectToChatAsync(factory);
        await using var bobChat = await bob.ConnectToChatAsync(factory);
        var message = await aliceChat.SendOkAsync(conversation.Id, "teh typo");

        var edited = await aliceChat.EditAsync(message.Id, "the typo");

        Assert.Null(edited.Error);
        Assert.NotNull(edited.Data!.EditedAt);
        Assert.Equal(message.Seq, edited.Data.Seq);
        Assert.True(edited.Data.ChangeSeq > message.ChangeSeq);
        var update = await bobChat.NextUpdateAsync();
        Assert.Equal(("the typo", edited.Data.ChangeSeq), (update.Content, update.ChangeSeq));
    }

    [Fact]
    public async Task Edits_and_deletes_show_up_in_the_reconnect_sync()
    {
        var alice = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("team");
        await using var chat = await alice.ConnectToChatAsync(factory);
        var first = await chat.SendOkAsync(conversation.Id, "first");
        var second = await chat.SendOkAsync(conversation.Id, "second");
        var seenUpTo = second.ChangeSeq;

        await chat.EditAsync(first.Id, "first, edited");
        await chat.DeleteAsync(second.Id);

        var changes = await alice.Client.GetFromJsonAsync<MessageChanges>(
            $"/conversations/{conversation.Id}/changes?after={seenUpTo}", TestUser.Json);
        Assert.Equal(
            [(first.Id, "first, edited", false), (second.Id, string.Empty, true)],
            changes!.Items.Select(m => (m.Id, m.Content, m.DeletedAt is not null)));
    }

    [Fact]
    public async Task Only_the_author_can_edit()
    {
        var alice = await TestUser.CreateAsync(factory);
        var bob = await TestUser.CreateAsync(factory);
        var outsider = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("team", bob);
        await alice.ChangeRoleAsync(conversation.Id, bob.Id, ConversationRole.Admin);
        await using var aliceChat = await alice.ConnectToChatAsync(factory);
        await using var bobChat = await bob.ConnectToChatAsync(factory);
        await using var outsiderChat = await outsider.ConnectToChatAsync(factory);
        var message = await aliceChat.SendOkAsync(conversation.Id, "mine");

        var byAdmin = await bobChat.EditAsync(message.Id, "not yours");
        var byOutsider = await outsiderChat.EditAsync(message.Id, "not yours either");

        Assert.Equal("not_message_author", byAdmin.Error?.Code);
        Assert.Equal("message_not_found", byOutsider.Error?.Code);
    }

    [Fact]
    public async Task Deleting_clears_the_content_and_keeps_the_thread()
    {
        var alice = await TestUser.CreateAsync(factory);
        var bob = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("team", bob);
        await using var aliceChat = await alice.ConnectToChatAsync(factory);
        await using var bobChat = await bob.ConnectToChatAsync(factory);
        var parent = await aliceChat.SendOkAsync(conversation.Id, "sensitive");
        await bobChat.ReplyOkAsync(conversation.Id, parent.Id, "reply");
        await bobChat.NextUpdateAsync();

        var deleted = await aliceChat.DeleteAsync(parent.Id);

        Assert.Equal(string.Empty, deleted.Data!.Content);
        Assert.NotNull(deleted.Data.DeletedAt);
        Assert.Equal(1, deleted.Data.ReplyCount);
        var update = await bobChat.NextUpdateAsync();
        Assert.Equal(string.Empty, update.Content);
        var replies = await bob.Client.GetFromJsonAsync<MessagePage>(
            $"/conversations/{conversation.Id}/messages/{parent.Id}/replies", TestUser.Json);
        Assert.Single(replies!.Items);
        Assert.Equal("message_deleted", (await aliceChat.EditAsync(parent.Id, "undo")).Error?.Code);
        Assert.Equal("message_deleted", (await bobChat.ReplyAsync(conversation.Id, parent.Id, "late")).Error?.Code);
    }

    [Fact]
    public async Task Admins_delete_any_message_but_members_only_their_own()
    {
        var alice = await TestUser.CreateAsync(factory);
        var bob = await TestUser.CreateAsync(factory);
        var carol = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("team", bob, carol);
        await alice.ChangeRoleAsync(conversation.Id, bob.Id, ConversationRole.Admin);
        await using var bobChat = await bob.ConnectToChatAsync(factory);
        await using var carolChat = await carol.ConnectToChatAsync(factory);
        await using var aliceChat = await alice.ConnectToChatAsync(factory);
        var byAlice = await aliceChat.SendOkAsync(conversation.Id, "owner's message");
        var byCarol = await carolChat.SendOkAsync(conversation.Id, "carol's message");

        var memberDeletesOther = await carolChat.DeleteAsync(byAlice.Id);
        var adminDeletesOther = await bobChat.DeleteAsync(byCarol.Id);

        Assert.Equal("cannot_delete_message", memberDeletesOther.Error?.Code);
        Assert.Null(adminDeletesOther.Error);
        Assert.NotNull(adminDeletesOther.Data!.DeletedAt);
    }

    [Fact]
    public async Task Deleting_twice_is_harmless_and_silent()
    {
        var alice = await TestUser.CreateAsync(factory);
        var bob = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("team", bob);
        await using var aliceChat = await alice.ConnectToChatAsync(factory);
        await using var bobChat = await bob.ConnectToChatAsync(factory);
        var message = await aliceChat.SendOkAsync(conversation.Id, "oops");
        var first = await aliceChat.DeleteAsync(message.Id);
        await bobChat.NextUpdateAsync();

        var second = await aliceChat.DeleteAsync(message.Id);

        Assert.Null(second.Error);
        Assert.Equal(first.Data!.ChangeSeq, second.Data!.ChangeSeq);
        await bobChat.AssertNoUpdateAsync(QuietPeriod);
    }
}
