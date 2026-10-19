using SharpChat.Domain.Messages;

namespace SharpChat.Application.Abstractions;

public interface IMessageRepository
{
    Task<Message?> GetByIdAsync(Guid messageId, CancellationToken cancellationToken);

    void Add(Message message);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
