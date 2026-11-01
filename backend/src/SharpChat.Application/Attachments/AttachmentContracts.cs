using SharpChat.Domain.Attachments;

namespace SharpChat.Application.Attachments;

public sealed record AttachmentDto(Guid Id, string FileName, string ContentType, AttachmentKind Kind, long SizeBytes)
{
    public static AttachmentDto From(Attachment attachment) => new(
        attachment.Id, attachment.FileName, attachment.ContentType, attachment.Kind, attachment.SizeBytes);
}

public sealed record AttachmentContent(AttachmentDto Attachment, Stream Content);
