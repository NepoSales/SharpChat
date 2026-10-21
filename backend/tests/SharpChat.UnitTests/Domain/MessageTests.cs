using SharpChat.Domain.Messages;

namespace SharpChat.UnitTests.Domain;

public sealed class MessageTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void New_message_starts_with_its_change_position_at_its_sequence()
    {
        var message = Message.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "hi", Now, seq: 7, maxContentLength: 10).Value!;

        Assert.Equal(7, message.Seq);
        Assert.Equal(7, message.ChangeSeq);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Sequence_numbers_start_at_one(long seq)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Message.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "hi", Now, seq, maxContentLength: 10));
    }
}
