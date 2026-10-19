using SharpChat.Application.Messages;

namespace SharpChat.Application.Abstractions;

public interface IChatNotifier
{
    Task MessageCreatedAsync(MessageDto message, IReadOnlyCollection<Guid> recipientIds, CancellationToken cancellationToken);
}
