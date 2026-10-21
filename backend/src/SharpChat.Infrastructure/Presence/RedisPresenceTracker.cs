using System.Globalization;
using Microsoft.Extensions.Options;
using SharpChat.Application.Abstractions;
using SharpChat.Application.Presence;
using StackExchange.Redis;

namespace SharpChat.Infrastructure.Presence;

internal sealed class RedisPresenceTracker(
    IConnectionMultiplexer redis,
    IOptions<PresenceOptions> options,
    TimeProvider time) : IPresenceTracker
{
    private const string ConnectScript = """
        redis.call('ZREMRANGEBYSCORE', KEYS[1], '-inf', ARGV[1])
        local before = redis.call('ZCARD', KEYS[1])
        redis.call('ZADD', KEYS[1], ARGV[2], ARGV[3])
        redis.call('PEXPIRE', KEYS[1], ARGV[4])
        return before
        """;

    private const string DisconnectScript = """
        redis.call('ZREMRANGEBYSCORE', KEYS[1], '-inf', ARGV[1])
        local removed = redis.call('ZREM', KEYS[1], ARGV[2])
        local after = redis.call('ZCARD', KEYS[1])
        if removed == 1 and after == 0 then
            return 1
        end
        return 0
        """;

    private TimeSpan Ttl => TimeSpan.FromSeconds(options.Value.ConnectionTtlSeconds);

    public async Task<bool> ConnectAsync(Guid userId, string connectionId, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var before = (long)await redis.GetDatabase().ScriptEvaluateAsync(
            ConnectScript,
            [Key(userId)],
            [Score(now), Score(now + Ttl), connectionId, (long)Ttl.TotalMilliseconds]);
        return before == 0;
    }

    public async Task<bool> DisconnectAsync(Guid userId, string connectionId, CancellationToken cancellationToken)
    {
        var wentOffline = (long)await redis.GetDatabase().ScriptEvaluateAsync(
            DisconnectScript,
            [Key(userId)],
            [Score(time.GetUtcNow()), connectionId]);
        return wentOffline == 1;
    }

    public async Task RefreshAsync(
        IReadOnlyCollection<(Guid UserId, string ConnectionId)> connections, CancellationToken cancellationToken)
    {
        if (connections.Count == 0)
        {
            return;
        }

        var expiresAt = Score(time.GetUtcNow() + Ttl);
        var batch = redis.GetDatabase().CreateBatch();
        var writes = connections
            .SelectMany(c => new Task[]
            {
                batch.SortedSetAddAsync(Key(c.UserId), c.ConnectionId, expiresAt),
                batch.KeyExpireAsync(Key(c.UserId), Ttl),
            })
            .ToList();
        batch.Execute();
        await Task.WhenAll(writes);
    }

    public async Task<IReadOnlySet<Guid>> GetOnlineAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        var now = Score(time.GetUtcNow());
        var batch = redis.GetDatabase().CreateBatch();
        var counts = userIds
            .Distinct()
            .Select(id => (Id: id, Count: batch.SortedSetLengthAsync(Key(id), now, double.PositiveInfinity, Exclude.Start)))
            .ToList();
        batch.Execute();
        await Task.WhenAll(counts.Select(c => c.Count));
        return counts.Where(c => c.Count.Result > 0).Select(c => c.Id).ToHashSet();
    }

    private static RedisKey Key(Guid userId) => $"sharpchat:presence:{userId:N}";

    private static double Score(DateTimeOffset at) => at.ToUnixTimeMilliseconds();
}
