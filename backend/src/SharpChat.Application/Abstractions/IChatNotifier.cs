using SharpChat.Application.Messages;
using SharpChat.Application.Presence;

namespace SharpChat.Application.Abstractions;

public interface IChatNotifier
{
    Task MessageCreatedAsync(MessageDto message, IReadOnlyCollection<Guid> recipientIds, CancellationToken cancellationToken);

    Task MessageUpdatedAsync(MessageDto message, IReadOnlyCollection<Guid> recipientIds, CancellationToken cancellationToken);

    Task ReceiptUpdatedAsync(ReceiptDto receipt, IReadOnlyCollection<Guid> recipientIds, CancellationToken cancellationToken);

    Task PresenceChangedAsync(PresenceDto presence, IReadOnlyCollection<Guid> recipientIds, CancellationToken cancellationToken);

    Task UserTypingAsync(TypingDto typing, IReadOnlyCollection<Guid> recipientIds, CancellationToken cancellationToken);
}
