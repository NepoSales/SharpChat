namespace SharpChat.Domain.Conversations;

public enum ReceiptKind
{
    Delivered,
    Read,
}

public readonly record struct ReceiptPosition(long Delivered, long Read)
{
    public ReceiptPosition Advance(ReceiptKind kind, long seq, long lastSeq)
    {
        var target = Math.Clamp(seq, 0, Math.Max(lastSeq, 0));
        var read = kind == ReceiptKind.Read ? Math.Max(Read, target) : Read;
        var delivered = Math.Max(Delivered, Math.Max(read, target));
        return new ReceiptPosition(delivered, read);
    }

    public bool IsAheadOf(ReceiptPosition other) => Delivered > other.Delivered || Read > other.Read;

    public long LowestChangedSince(ReceiptPosition previous) =>
        (Delivered > previous.Delivered, Read > previous.Read) switch
        {
            (true, true) => Math.Min(previous.Delivered, previous.Read),
            (true, false) => previous.Delivered,
            (false, true) => previous.Read,
            _ => Math.Max(Delivered, Read),
        };
}
