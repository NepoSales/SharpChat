using SharpChat.Application.Abstractions;
using SharpChat.Domain.Common;
using SharpChat.Domain.Conversations;
using SharpChat.Domain.Messages;

namespace SharpChat.Application.Messages;

public sealed class ReceiptService(IReceiptStore receipts, IChatNotifier notifier)
{
    public async Task<Result<ReceiptDto>> MarkAsync(
        Guid userId, MarkReceiptRequest request, ReceiptKind kind, CancellationToken cancellationToken)
    {
        var current = await receipts.GetAsync(request.ConversationId, userId, cancellationToken);
        if (current is not { } previous)
        {
            return ConversationErrors.NotFound;
        }

        if (request.Seq < 0)
        {
            return MessageErrors.InvalidSeq;
        }

        var lastSeq = await receipts.GetLastSeqAsync(request.ConversationId, cancellationToken);
        var next = previous.Advance(kind, request.Seq, lastSeq);
        var receipt = new ReceiptDto(request.ConversationId, userId, next.Delivered, next.Read);
        if (!next.IsAheadOf(previous))
        {
            return receipt;
        }

        await receipts.AdvanceAsync(request.ConversationId, userId, next, cancellationToken);

        var senders = await receipts.ListSendersAsync(
            request.ConversationId, next.LowestChangedSince(previous), next.Delivered, cancellationToken);
        var recipients = senders.Append(userId).Distinct().ToList();
        await notifier.ReceiptUpdatedAsync(receipt, recipients, cancellationToken);

        return receipt;
    }
}
