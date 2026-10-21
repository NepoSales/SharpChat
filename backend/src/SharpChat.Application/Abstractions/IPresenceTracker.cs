namespace SharpChat.Application.Abstractions;

public interface IPresenceTracker
{
    Task<bool> ConnectAsync(Guid userId, string connectionId, CancellationToken cancellationToken);

    Task<bool> DisconnectAsync(Guid userId, string connectionId, CancellationToken cancellationToken);

    Task RefreshAsync(IReadOnlyCollection<(Guid UserId, string ConnectionId)> connections, CancellationToken cancellationToken);

    Task<IReadOnlySet<Guid>> GetOnlineAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
}
