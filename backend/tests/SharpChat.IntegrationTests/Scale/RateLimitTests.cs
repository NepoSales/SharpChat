using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using SharpChat.IntegrationTests.Auth;
using SharpChat.IntegrationTests.Conversations;
using SharpChat.IntegrationTests.Infrastructure;
using SharpChat.IntegrationTests.Messaging;

namespace SharpChat.IntegrationTests.Scale;

[Collection(ApiTestGroup.Name)]
public sealed class RateLimitTests(SharpChatApiFactory factory)
{
    private WebApplicationFactory<Program> WithLimit(string rule, int permits) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RateLimiting:KeyPrefix", $"test:{Guid.NewGuid():N}");
            builder.UseSetting($"RateLimiting:{rule}:PermitLimit", permits.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.UseSetting($"RateLimiting:{rule}:WindowSeconds", "60");
        });

    [Fact]
    public async Task Login_attempts_over_the_limit_get_429_with_retry_after()
    {
        await using var limited = WithLimit("Auth", 3);
        var client = limited.CreateClient();

        var statuses = new List<HttpStatusCode>();
        HttpResponseMessage last = null!;
        for (var i = 0; i < 4; i++)
        {
            last = await client.LoginAsync("nobody@example.com", "Wrong1234");
            statuses.Add(last.StatusCode);
        }

        Assert.Equal(
            [HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests],
            statuses);
        Assert.Equal("rate_limited", await last.ErrorCodeAsync());
        Assert.NotNull(last.Headers.RetryAfter);
    }

    [Fact]
    public async Task Each_user_has_its_own_api_budget()
    {
        await using var limited = WithLimit("Api", 4);
        var alice = await TestUser.CreateAsync(limited);
        var bob = await TestUser.CreateAsync(limited);

        var aliceStatuses = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            aliceStatuses.Add((await alice.Client.GetAsync("/conversations")).StatusCode);
        }

        var bobStatus = (await bob.Client.GetAsync("/conversations")).StatusCode;

        Assert.Equal(HttpStatusCode.TooManyRequests, aliceStatuses[^1]);
        Assert.Equal(HttpStatusCode.OK, bobStatus);
    }

    [Fact]
    public async Task Health_checks_are_never_limited()
    {
        await using var limited = WithLimit("Api", 1);
        var client = limited.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
        {
            statuses.Add((await client.GetAsync("/health")).StatusCode);
        }

        Assert.All(statuses, status => Assert.Equal(HttpStatusCode.OK, status));
    }

    [Fact]
    public async Task Hub_messages_over_the_limit_are_rejected_with_a_code()
    {
        await using var limited = WithLimit("HubMessages", 3);
        var alice = await TestUser.CreateAsync(limited);
        var conversation = await alice.CreateConversationOkAsync("busy");
        await using var chat = await alice.ConnectToChatAsync(limited);

        var responses = new List<string?>();
        for (var i = 0; i < 4; i++)
        {
            responses.Add((await chat.SendAsync(conversation.Id, $"m{i}")).Error?.Code);
        }

        Assert.Equal([null, null, null, "rate_limited"], responses);
    }

    [Fact]
    public async Task Typing_over_the_limit_is_dropped_silently()
    {
        await using var limited = WithLimit("Typing", 1);
        var alice = await TestUser.CreateAsync(limited);
        var bob = await TestUser.CreateAsync(limited);
        var conversation = await alice.CreateConversationOkAsync("team", bob);
        await using var aliceChat = await alice.ConnectToChatAsync(limited);
        await using var bobChat = await bob.ConnectToChatAsync(limited);

        await aliceChat.TypingAsync(conversation.Id);
        await aliceChat.TypingAsync(conversation.Id);

        await bobChat.NextTypingAsync();
        await bobChat.AssertNoTypingAsync(TimeSpan.FromMilliseconds(500));
    }
}
