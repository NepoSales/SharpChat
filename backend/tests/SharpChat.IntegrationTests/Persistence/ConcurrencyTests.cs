using Microsoft.Extensions.DependencyInjection;
using SharpChat.Application.Abstractions;
using SharpChat.Application.Common;
using SharpChat.Domain.Messages;
using SharpChat.IntegrationTests.Conversations;
using SharpChat.IntegrationTests.Infrastructure;

namespace SharpChat.IntegrationTests.Persistence;

[Collection(ApiTestGroup.Name)]
public sealed class ConcurrencyTests(SharpChatApiFactory factory)
{
    [Fact]
    public async Task Stale_membership_changes_conflict_instead_of_overwriting()
    {
        var owner = await TestUser.CreateAsync(factory);
        var first = await TestUser.CreateAsync(factory);
        var second = await TestUser.CreateAsync(factory);
        var conversation = await owner.CreateConversationOkAsync("team");
        await using var scopeA = factory.Services.CreateAsyncScope();
        await using var scopeB = factory.Services.CreateAsyncScope();
        var repositoryA = scopeA.ServiceProvider.GetRequiredService<IConversationRepository>();
        var repositoryB = scopeB.ServiceProvider.GetRequiredService<IConversationRepository>();
        var loadedA = (await repositoryA.GetByIdAsync(conversation.Id, CancellationToken.None))!;
        var loadedB = (await repositoryB.GetByIdAsync(conversation.Id, CancellationToken.None))!;

        loadedA.AddMember(owner.Id, first.Id, 100, DateTimeOffset.UtcNow);
        loadedB.AddMember(owner.Id, second.Id, 100, DateTimeOffset.UtcNow);
        await repositoryA.SaveChangesAsync(CancellationToken.None);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => repositoryB.SaveChangesAsync(CancellationToken.None));
        var final = await owner.GetConversationAsync(conversation.Id);
        Assert.DoesNotContain(second.UserName, await final.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Inserting_an_existing_message_id_is_reported_as_a_duplicate()
    {
        var alice = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("team");
        var messageId = Guid.NewGuid();
        await using var firstScope = factory.Services.CreateAsyncScope();
        await using var secondScope = factory.Services.CreateAsyncScope();
        var first = firstScope.ServiceProvider.GetRequiredService<IMessageRepository>();
        var second = secondScope.ServiceProvider.GetRequiredService<IMessageRepository>();
        first.Add(Message.Create(messageId, conversation.Id, alice.Id, "first", DateTimeOffset.UtcNow, 1, 4000).Value!);
        await first.SaveChangesAsync(CancellationToken.None);

        second.Add(Message.Create(messageId, conversation.Id, alice.Id, "again", DateTimeOffset.UtcNow, 2, 4000).Value!);

        await Assert.ThrowsAsync<DuplicateKeyException>(() => second.SaveChangesAsync(CancellationToken.None));
        Assert.Equal("first", (await second.GetByIdAsync(messageId, CancellationToken.None))!.Content);
    }
}
