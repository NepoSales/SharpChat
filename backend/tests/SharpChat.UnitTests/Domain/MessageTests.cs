using SharpChat.Domain.Conversations;
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

    private const int MaxLength = 100;

    private readonly Guid _author = Guid.NewGuid();
    private readonly Guid _other = Guid.NewGuid();

    private Message NewMessage(long seq = 1) =>
        Message.Create(Guid.NewGuid(), Guid.NewGuid(), _author, "original", Now, seq, MaxLength).Value!;

    [Fact]
    public void Replying_links_the_reply_and_updates_the_parent()
    {
        var parent = NewMessage();

        var reply = parent.Reply(Guid.NewGuid(), _other, "answer", Now.AddMinutes(1), seq: 2, parentChangeSeq: 3, MaxLength).Value!;

        Assert.Equal(parent.Id, reply.ParentMessageId);
        Assert.Equal(parent.ConversationId, reply.ConversationId);
        Assert.Equal((2L, 2L), (reply.Seq, reply.ChangeSeq));
        Assert.Equal(1, parent.ReplyCount);
        Assert.Equal(Now.AddMinutes(1), parent.LastReplyAt);
        Assert.Equal(2, parent.LastReplySeq);
        Assert.Equal(3, parent.ChangeSeq);
    }

    [Fact]
    public void Threads_have_one_level_only()
    {
        var parent = NewMessage();
        var reply = parent.Reply(Guid.NewGuid(), _other, "answer", Now, 2, 3, MaxLength).Value!;

        var nested = reply.Reply(Guid.NewGuid(), _author, "deeper", Now, 4, 5, MaxLength);

        Assert.Equal(MessageErrors.NestedReply, nested.Error);
    }

    [Fact]
    public void Deleted_messages_cannot_get_replies_or_edits()
    {
        var message = NewMessage();
        message.Delete(_author, ConversationRole.Member, Now, 2);

        Assert.Equal(MessageErrors.Deleted, message.Reply(Guid.NewGuid(), _other, "late", Now, 3, 4, MaxLength).Error);
        Assert.Equal(MessageErrors.Deleted, message.Edit(_author, "changed", Now, 3, MaxLength).Error);
    }

    [Fact]
    public void A_failed_reply_leaves_the_parent_untouched()
    {
        var parent = NewMessage();

        var reply = parent.Reply(Guid.NewGuid(), _other, "   ", Now, 2, 3, MaxLength);

        Assert.False(reply.IsSuccess);
        Assert.Equal((0, 1L), (parent.ReplyCount, parent.ChangeSeq));
    }

    [Fact]
    public void Only_the_author_edits()
    {
        var message = NewMessage();

        Assert.Equal(MessageErrors.NotAuthor, message.Edit(_other, "hijack", Now, 2, MaxLength).Error);
        Assert.True(message.Edit(_author, "fixed typo", Now.AddMinutes(2), 2, MaxLength).IsSuccess);
        Assert.Equal(("fixed typo", 2L, Now.AddMinutes(2)), (message.Content, message.ChangeSeq, message.EditedAt!.Value));
    }

    [Fact]
    public void Edits_follow_the_content_rules()
    {
        var message = NewMessage();

        Assert.False(message.Edit(_author, " ", Now, 2, MaxLength).IsSuccess);
        Assert.False(message.Edit(_author, new string('a', MaxLength + 1), Now, 2, MaxLength).IsSuccess);
        Assert.Equal("original", message.Content);
    }

    [Theory]
    [InlineData(ConversationRole.Admin)]
    [InlineData(ConversationRole.Owner)]
    public void Admins_and_owners_delete_any_message(ConversationRole role)
    {
        var message = NewMessage();

        Assert.True(message.Delete(_other, role, Now, 2).IsSuccess);
        Assert.Equal((string.Empty, 2L), (message.Content, message.ChangeSeq));
        Assert.True(message.IsDeleted);
    }

    [Fact]
    public void Members_delete_only_their_own_messages()
    {
        var message = NewMessage();

        Assert.Equal(MessageErrors.CannotDelete, message.Delete(_other, ConversationRole.Member, Now, 2).Error);
        Assert.True(message.Delete(_author, ConversationRole.Member, Now, 2).IsSuccess);
    }

    [Fact]
    public void Deleting_twice_changes_nothing_the_second_time()
    {
        var message = NewMessage();
        message.Delete(_author, ConversationRole.Member, Now, 2);

        Assert.True(message.Delete(_author, ConversationRole.Member, Now.AddMinutes(1), 3).IsSuccess);
        Assert.Equal((2L, Now), (message.ChangeSeq, message.DeletedAt!.Value));
    }
}
