using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace SharpChat.Api.RateLimiting;

internal sealed class HubRateLimiters : IDisposable
{
    public HubRateLimiters(IConnectionMultiplexer redis, IOptions<RateLimitingOptions> options)
    {
        var limits = options.Value;
        Messages = PartitionedRateLimiter.Create<string, string>(userId =>
            RateLimitingSetup.Partition(redis, $"{limits.KeyPrefix}:hub:messages:{userId}", limits.HubMessages));
        Typing = PartitionedRateLimiter.Create<string, string>(userId =>
            RateLimitingSetup.Partition(redis, $"{limits.KeyPrefix}:hub:typing:{userId}", limits.Typing));
    }

    public PartitionedRateLimiter<string> Messages { get; }

    public PartitionedRateLimiter<string> Typing { get; }

    public void Dispose()
    {
        Messages.Dispose();
        Typing.Dispose();
    }
}
