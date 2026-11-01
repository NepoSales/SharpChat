using SharpChat.Domain.Attachments;
using SharpChat.Domain.Messages;

namespace SharpChat.UnitTests.Domain;

public sealed class AttachmentTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly Guid _conversation = Guid.NewGuid();
    private readonly Guid _uploader = Guid.NewGuid();

    private Attachment NewAttachment() => Attachment.Create(
        Guid.NewGuid(), _conversation, _uploader, "a.png", "image/png", AttachmentKind.Image, 10, "key", Now);

    [Fact]
    public void A_new_upload_is_pending_and_readable_only_by_its_uploader()
    {
        var attachment = NewAttachment();

        Assert.True(attachment.IsPending);
        Assert.True(attachment.CanBeReadBy(_uploader));
        Assert.False(attachment.CanBeReadBy(Guid.NewGuid()));
    }

    [Fact]
    public void Linking_makes_it_readable_by_everyone_in_the_conversation()
    {
        var attachment = NewAttachment();
        var messageId = Guid.NewGuid();

        var linked = attachment.LinkTo(messageId, _conversation, _uploader);

        Assert.True(linked.IsSuccess);
        Assert.Equal(messageId, attachment.MessageId);
        Assert.True(attachment.CanBeReadBy(Guid.NewGuid()));
    }

    [Fact]
    public void It_links_only_once_only_by_the_uploader_and_only_in_its_conversation()
    {
        var attachment = NewAttachment();

        var bySomeoneElse = attachment.LinkTo(Guid.NewGuid(), _conversation, Guid.NewGuid());
        var elsewhere = attachment.LinkTo(Guid.NewGuid(), Guid.NewGuid(), _uploader);
        attachment.LinkTo(Guid.NewGuid(), _conversation, _uploader);
        var again = attachment.LinkTo(Guid.NewGuid(), _conversation, _uploader);

        Assert.All([bySomeoneElse, elsewhere, again], r => Assert.Equal(AttachmentErrors.Unavailable, r.Error));
    }

    [Fact]
    public void A_deleted_attachment_cannot_be_read_or_linked()
    {
        var attachment = NewAttachment();

        attachment.Delete(Now);
        attachment.Delete(Now.AddMinutes(1));

        Assert.Equal(Now, attachment.DeletedAt);
        Assert.False(attachment.CanBeReadBy(_uploader));
        Assert.False(attachment.LinkTo(Guid.NewGuid(), _conversation, _uploader).IsSuccess);
    }

    [Fact]
    public void A_message_with_attachments_may_have_no_text_and_be_edited_to_no_text()
    {
        var withFiles = Message.Create(Guid.NewGuid(), _conversation, _uploader, "", Now, 1, 100, hasAttachments: true);
        var withoutFiles = Message.Create(Guid.NewGuid(), _conversation, _uploader, "", Now, 1, 100);
        var edited = Message.Create(Guid.NewGuid(), _conversation, _uploader, "caption", Now, 2, 100, hasAttachments: true).Value!
            .Edit(_uploader, " ", Now, 3, 100);

        Assert.True(withFiles.IsSuccess);
        Assert.Equal(MessageErrors.EmptyContent, withoutFiles.Error);
        Assert.True(edited.IsSuccess);
    }
}
