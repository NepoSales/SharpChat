using System.Collections.Concurrent;

namespace SharpChat.Api.Hubs;

public sealed class ConnectionRegistry
{
    private readonly ConcurrentDictionary<string, Guid> _connections = new();

    public void Add(string connectionId, Guid userId) => _connections[connectionId] = userId;

    public void Remove(string connectionId) => _connections.TryRemove(connectionId, out _);

    public IReadOnlyCollection<(Guid UserId, string ConnectionId)> Snapshot() =>
        _connections.Select(pair => (pair.Value, pair.Key)).ToList();
}
