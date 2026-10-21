using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using SharpChat.IntegrationTests.Conversations;
using SharpChat.IntegrationTests.Infrastructure;
using SharpChat.IntegrationTests.Messaging;

namespace SharpChat.IntegrationTests.Scale;

[Collection(ApiTestGroup.Name)]
public sealed class MultiInstanceTests(SharpChatApiFactory factory)
{
    private WebApplicationFactory<Program> SecondInstance() =>
        factory.WithWebHostBuilder(builder => builder.UseSetting("InstanceName", "instance-b"));

    [Fact]
    public async Task A_message_sent_on_one_instance_reaches_a_client_on_another()
    {
        await using var instanceB = SecondInstance();
        var alice = await TestUser.CreateAsync(factory);
        var bob = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("team", bob);
        await using var aliceOnA = await alice.ConnectToChatAsync(factory);
        await using var bobOnB = await bob.ConnectToChatAsync(instanceB);

        var sent = await aliceOnA.SendOkAsync(conversation.Id, "hello from instance A");

        var received = await bobOnB.NextMessageAsync();
        Assert.Equal((sent.Id, "hello from instance A"), (received.Id, received.Content));
    }

    [Fact]
    public async Task Edits_and_receipts_cross_instances_too()
    {
        await using var instanceB = SecondInstance();
        var alice = await TestUser.CreateAsync(factory);
        var bob = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("team", bob);
        await using var aliceOnA = await alice.ConnectToChatAsync(factory);
        await using var bobOnB = await bob.ConnectToChatAsync(instanceB);
        var message = await aliceOnA.SendOkAsync(conversation.Id, "draft");
        await bobOnB.NextMessageAsync();

        await aliceOnA.EditAsync(message.Id, "final");
        var edit = await bobOnB.NextUpdateAsync();
        await bobOnB.MarkReadAsync(conversation.Id, message.Seq);
        var receipt = await aliceOnA.NextReceiptAsync();

        Assert.Equal("final", edit.Content);
        Assert.Equal((bob.Id, message.Seq), (receipt.UserId, receipt.LastReadSeq));
    }
}
