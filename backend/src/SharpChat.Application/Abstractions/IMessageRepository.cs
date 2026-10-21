using SharpChat.Domain.Messages;

namespace SharpChat.Application.Abstractions;

public interface IMessageRepository
{
    Task<ISequencedWrite> BeginSequencedWriteAsync(Guid conversationId, CancellationToken cancellationToken);

    Task<Message?> GetByIdAsync(Guid messageId, CancellationToken cancellationToken);

    void Add(Message message);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface ISequencedWrite : IAsyncDisposable
{
    long TakeSeq();

    Task CommitAsync(CancellationToken cancellationToken);
}
