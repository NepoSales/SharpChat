using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using SharpChat.Application.Diagnostics;
using SharpChat.IntegrationTests.Conversations;
using SharpChat.IntegrationTests.Infrastructure;
using SharpChat.IntegrationTests.Messaging;

namespace SharpChat.IntegrationTests.Observability;

[Collection(ApiTestGroup.Name)]
public sealed class MetricsTests(SharpChatApiFactory factory)
{
    private static readonly TimeSpan MeasurementTimeout = TimeSpan.FromSeconds(5);

    private WebApplicationFactory<Program> IsolatedHost(int hubMessagesPerMinute = 100000) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RateLimiting:KeyPrefix", $"test:{Guid.NewGuid():N}");
            builder.UseSetting("RateLimiting:HubMessages:PermitLimit", hubMessagesPerMinute.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.UseSetting("RateLimiting:HubMessages:WindowSeconds", "60");
        });

    private static MetricCollector<T> Collect<T>(WebApplicationFactory<Program> host, string instrument)
        where T : struct
    {
        host.Services.GetRequiredService<ChatMetrics>();
        return new MetricCollector<T>(host.Services.GetRequiredService<IMeterFactory>(), ChatMetrics.MeterName, instrument);
    }

    private static long Sum(MetricCollector<long> collector, string tag, string value) =>
        collector.GetMeasurementSnapshot().Where(m => Equals(m.Tags[tag], value)).Sum(m => m.Value);

    [Fact]
    public async Task Sends_retries_edits_and_deletes_are_measured()
    {
        await using var host = IsolatedHost();
        using var sent = Collect<long>(host, "sharpchat.messages.sent");
        using var duration = Collect<double>(host, "sharpchat.messages.send.duration");
        using var changed = Collect<long>(host, "sharpchat.messages.changed");
        var alice = await TestUser.CreateAsync(host);
        var conversation = await alice.CreateConversationOkAsync("team");
        await using var chat = await alice.ConnectToChatAsync(host);

        var messageId = Guid.NewGuid();
        await chat.SendAsync(conversation.Id, "hello", messageId);
        await chat.SendAsync(conversation.Id, "hello", messageId);
        await chat.ReplyOkAsync(conversation.Id, messageId, "in a thread");
        await chat.SendAsync(conversation.Id, " ");
        await chat.EditAsync(messageId, "hello, edited");
        await chat.DeleteAsync(messageId);

        Assert.Equal(1, Sum(sent, "sharpchat.message.kind", "message"));
        Assert.Equal(1, Sum(sent, "sharpchat.message.kind", "reply"));
        var outcomes = duration.GetMeasurementSnapshot()
            .GroupBy(m => (string)m.Tags["sharpchat.send.outcome"]!)
            .ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(new Dictionary<string, int> { ["created"] = 2, ["duplicate"] = 1, ["rejected"] = 1 }, outcomes);
        Assert.All(duration.GetMeasurementSnapshot(), m => Assert.True(m.Value > 0));
        Assert.Equal(1, Sum(changed, "sharpchat.message.change", "edited"));
        Assert.Equal(1, Sum(changed, "sharpchat.message.change", "deleted"));
    }

    [Fact]
    public async Task Presence_transitions_are_counted_once_per_user()
    {
        await using var host = IsolatedHost();
        using var transitions = Collect<long>(host, "sharpchat.presence.transitions");
        var bob = await TestUser.CreateAsync(host);

        var firstTab = await bob.ConnectToChatAsync(host);
        var secondTab = await bob.ConnectToChatAsync(host);
        await firstTab.DisposeAsync();
        await secondTab.DisposeAsync();
        await transitions.WaitForMeasurementsAsync(2, MeasurementTimeout);

        Assert.Equal(1, Sum(transitions, "sharpchat.presence.state", "online"));
        Assert.Equal(1, Sum(transitions, "sharpchat.presence.state", "offline"));
    }

    [Fact]
    public async Task Hub_rate_limit_rejections_are_counted_per_method()
    {
        await using var host = IsolatedHost(hubMessagesPerMinute: 1);
        using var rejected = Collect<long>(host, "sharpchat.hub.rate_limited");
        var alice = await TestUser.CreateAsync(host);
        var conversation = await alice.CreateConversationOkAsync("busy");
        await using var chat = await alice.ConnectToChatAsync(host);

        await chat.SendAsync(conversation.Id, "first");
        await chat.SendAsync(conversation.Id, "second");

        Assert.Equal(1, Sum(rejected, "sharpchat.hub.method", "SendMessage"));
    }
}
