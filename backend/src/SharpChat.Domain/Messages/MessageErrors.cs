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

    public static readonly Error NotFound = Error.NotFound("message_not_found", "Message not found.");

    public static readonly Error ParentNotFound =
        Error.NotFound("parent_message_not_found", "The message you are replying to was not found.");

    public static readonly Error NestedReply =
        Error.Conflict("nested_reply", "Replies can't have their own thread. Reply to the original message instead.");

    public static readonly Error Deleted = Error.Conflict("message_deleted", "This message was deleted.");

    public static readonly Error NotAuthor = Error.Forbidden("not_message_author", "Only the author can edit this message.");

    public static readonly Error CannotDelete =
        Error.Forbidden("cannot_delete_message", "Only the author, an admin or the owner can delete this message.");

    public static Error ContentTooLong(int maxLength) =>
        Error.Validation("Content", $"Message content can be at most {maxLength} characters.");
}
