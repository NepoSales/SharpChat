using SharpChat.Domain.Common;

namespace SharpChat.Domain.Attachments;

public static class AttachmentErrors
{
    public static readonly Error NotFound = Error.NotFound("attachment_not_found", "Attachment not found.");

    public static readonly Error Unavailable = Error.Conflict(
        "attachment_unavailable",
        "One or more attachments can't be sent. Upload them again to this conversation.");

    public static readonly Error Empty = Error.Validation("File", "The file is empty.");

    public static readonly Error UnsupportedType = Error.Validation(
        "File", "This file type isn't supported. Send images (PNG, JPEG, GIF, WebP), PDF, ZIP or text files.");

    public static Error TooLarge(long maxBytes) =>
        Error.Validation("File", $"The file can be at most {maxBytes / (1024 * 1024)} MB.");

    public static Error TooMany(int max) =>
        Error.Validation("AttachmentIds", $"A message can have at most {max} attachments.");
}
