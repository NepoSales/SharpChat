using SharpChat.Domain.Common;

namespace SharpChat.Domain.Messages;

public static class MessageErrors
{
    public static readonly Error InvalidId = Error.Validation("MessageId", "Message id is required.");

    public static readonly Error EmptyContent = Error.Validation("Content", "Message content can't be empty.");

    public static readonly Error IdConflict =
        Error.Conflict("message_id_conflict", "This message id is already used by another message.");

    public static readonly Error InvalidCursor = Error.Validation("Before", "The cursor is invalid.");

    public static readonly Error InvalidSeq = Error.Validation("Seq", "Sequence numbers can't be negative.");

    public static Error ContentTooLong(int maxLength) =>
        Error.Validation("Content", $"Message content can be at most {maxLength} characters.");
}
