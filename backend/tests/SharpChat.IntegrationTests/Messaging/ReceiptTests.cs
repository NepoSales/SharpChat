using System.Net.Http.Json;
using SharpChat.Application.Conversations;
using SharpChat.IntegrationTests.Conversations;
using SharpChat.IntegrationTests.Infrastructure;

namespace SharpChat.IntegrationTests.Messaging;

[Collection(ApiTestGroup.Name)]
public sealed class ReceiptTests(SharpChatApiFactory factory)
{
    private static readonly TimeSpan QuietPeriod = TimeSpan.FromMilliseconds(500);

    [Fact]
    public async Task Receipts_reach_the_authors_of_the_messages_and_the_reader_only()
    {
        var alice = await TestUser.CreateAsync(factory);
        var bob = await TestUser.CreateAsync(factory);
        var carol = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("team", bob, carol);
        await using var aliceChat = await alice.ConnectToChatAsync(factory);
        await using var bobChat = await bob.ConnectToChatAsync(factory);
        await using var carolChat = await carol.ConnectToChatAsync(factory);
        var message = await aliceChat.SendOkAsync(conversation.Id, "did you get this?");

        var response = await bobChat.MarkDeliveredAsync(conversation.Id, message.Seq);

        Assert.Null(response.Error);
        var toAlice = await aliceChat.NextReceiptAsync();
        Assert.Equal((bob.Id, message.Seq, 0L), (toAlice.UserId, toAlice.LastDeliveredSeq, toAlice.LastReadSeq));
        Assert.Equal(bob.Id, (await bobChat.NextReceiptAsync()).UserId);
        await carolChat.AssertNoReceiptAsync(QuietPeriod);
    }

    [Fact]
    public async Task Read_positions_imply_delivery_are_capped_and_never_move_back()
    {
        var alice = await TestUser.CreateAsync(factory);
        var bob = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("team", bob);
        await using var aliceChat = await alice.ConnectToChatAsync(factory);
        await using var bobChat = await bob.ConnectToChatAsync(factory);
        await aliceChat.SendOkAsync(conversation.Id, "one");
        await aliceChat.SendOkAsync(conversation.Id, "two");

        var ahead = await bobChat.MarkReadAsync(conversation.Id, 999);
        var back = await bobChat.MarkReadAsync(conversation.Id, 1);

        Assert.Equal((2L, 2L), (ahead.Data!.LastDeliveredSeq, ahead.Data.LastReadSeq));
        Assert.Equal((2L, 2L), (back.Data!.LastDeliveredSeq, back.Data.LastReadSeq));
        var details = await (await alice.GetConversationAsync(conversation.Id)).ReadAsAsync<ConversationDto>();
        var bobMember = details.Members.Single(m => m.UserId == bob.Id);
        Assert.Equal((2L, 2L), (bobMember.LastDeliveredSeq, bobMember.LastReadSeq));
    }

    [Fact]
    public async Task A_receipt_that_changes_nothing_notifies_nobody()
    {
        var alice = await TestUser.CreateAsync(factory);
        var bob = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("team", bob);
        await using var aliceChat = await alice.ConnectToChatAsync(factory);
        await using var bobChat = await bob.ConnectToChatAsync(factory);
        var message = await aliceChat.SendOkAsync(conversation.Id, "hi");
        await bobChat.MarkReadAsync(conversation.Id, message.Seq);
        await aliceChat.NextReceiptAsync();

        await bobChat.MarkDeliveredAsync(conversation.Id, message.Seq);

        await aliceChat.AssertNoReceiptAsync(QuietPeriod);
    }

    [Fact]
    public async Task Non_members_cannot_send_receipts()
    {
        var alice = await TestUser.CreateAsync(factory);
        var outsider = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("private");
        await using var outsiderChat = await outsider.ConnectToChatAsync(factory);

        var response = await outsiderChat.MarkReadAsync(conversation.Id, 1);

        Assert.Equal("conversation_not_found", response.Error?.Code);
    }

    [Fact]
    public async Task Unread_count_ignores_own_messages_and_follows_the_read_position()
    {
        var alice = await TestUser.CreateAsync(factory);
        var bob = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("team", bob);
        await using var aliceChat = await alice.ConnectToChatAsync(factory);
        await using var bobChat = await bob.ConnectToChatAsync(factory);
        await aliceChat.SendOkAsync(conversation.Id, "1");
        await aliceChat.SendOkAsync(conversation.Id, "2");
        await aliceChat.SendOkAsync(conversation.Id, "3");
        await bobChat.SendOkAsync(conversation.Id, "mine");

        var before = await SummaryAsync(bob, conversation.Id);
        await bobChat.MarkReadAsync(conversation.Id, 2);
        var after = await SummaryAsync(bob, conversation.Id);

        Assert.Equal((3, 4L), (before.UnreadCount, before.LastSeq));
        Assert.Equal(1, after.UnreadCount);
        Assert.Equal(1, (await SummaryAsync(alice, conversation.Id)).UnreadCount);
    }

    private static async Task<ConversationSummaryDto> SummaryAsync(TestUser user, Guid conversationId)
    {
        var list = await user.Client.GetFromJsonAsync<List<ConversationSummaryDto>>("/conversations", TestUser.Json);
        return list!.Single(c => c.Id == conversationId);
    }
}
