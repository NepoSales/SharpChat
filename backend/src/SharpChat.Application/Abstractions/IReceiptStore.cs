using SharpChat.Domain.Conversations;

namespace SharpChat.Application.Abstractions;

public interface IReceiptStore
{
    Task<ReceiptPosition?> GetAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken);

    Task<long> GetLastSeqAsync(Guid conversationId, CancellationToken cancellationToken);

    Task AdvanceAsync(Guid conversationId, Guid userId, ReceiptPosition position, CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> ListSendersAsync(
        Guid conversationId, long afterSeq, long upToSeq, CancellationToken cancellationToken);
}
