using SharpChat.Domain.Conversations;

namespace SharpChat.UnitTests.Domain;

public sealed class ReceiptPositionTests
{
    [Fact]
    public void Reading_also_marks_as_delivered()
    {
        var next = new ReceiptPosition(2, 1).Advance(ReceiptKind.Read, 5, lastSeq: 10);

        Assert.Equal(new ReceiptPosition(5, 5), next);
    }

    [Fact]
    public void Delivery_does_not_move_the_read_position()
    {
        var next = new ReceiptPosition(2, 1).Advance(ReceiptKind.Delivered, 7, lastSeq: 10);

        Assert.Equal(new ReceiptPosition(7, 1), next);
    }

    [Theory]
    [InlineData(ReceiptKind.Delivered)]
    [InlineData(ReceiptKind.Read)]
    public void Positions_never_move_back(ReceiptKind kind)
    {
        var current = new ReceiptPosition(8, 6);

        Assert.Equal(current, current.Advance(kind, 3, lastSeq: 10));
    }

    [Fact]
    public void Positions_are_capped_at_the_last_message()
    {
        var next = new ReceiptPosition(0, 0).Advance(ReceiptKind.Read, long.MaxValue, lastSeq: 4);

        Assert.Equal(new ReceiptPosition(4, 4), next);
    }

    [Fact]
    public void Negative_positions_are_ignored()
    {
        var current = new ReceiptPosition(1, 1);

        Assert.Equal(current, current.Advance(ReceiptKind.Read, -5, lastSeq: 4));
    }

    [Fact]
    public void Ahead_only_when_some_position_moved_forward()
    {
        var current = new ReceiptPosition(3, 2);

        Assert.True(new ReceiptPosition(4, 2).IsAheadOf(current));
        Assert.True(new ReceiptPosition(3, 3).IsAheadOf(current));
        Assert.False(current.IsAheadOf(current));
    }

    [Theory]
    [InlineData(3, 2, 6, 2, 3)]
    [InlineData(3, 2, 3, 3, 2)]
    [InlineData(5, 2, 9, 9, 2)]
    [InlineData(3, 2, 3, 2, 3)]
    public void Lowest_changed_position_bounds_the_messages_whose_status_changed(
        long oldDelivered, long oldRead, long newDelivered, long newRead, long expected)
    {
        var lowest = new ReceiptPosition(newDelivered, newRead).LowestChangedSince(new ReceiptPosition(oldDelivered, oldRead));

        Assert.Equal(expected, lowest);
    }
}
