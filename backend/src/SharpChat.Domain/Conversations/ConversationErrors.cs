using SharpChat.Domain.Common;

namespace SharpChat.Domain.Conversations;

public static class ConversationErrors
{
    public static readonly Error NotFound = Error.NotFound("conversation_not_found", "Conversation not found.");

    public static readonly Error MemberNotFound = Error.NotFound("member_not_found", "This user is not a member of the conversation.");

    public static readonly Error AlreadyMember = Error.Conflict("already_member", "This user is already a member of the conversation.");

    public static readonly Error OwnerCannotLeave = Error.Conflict("owner_cannot_leave", "The owner can't leave or be removed from the conversation.");

    public static readonly Error CannotManageMembers = Error.Forbidden("cannot_manage_members", "Only owners and admins can manage members.");

    public static readonly Error CannotRemoveMember = Error.Forbidden("cannot_remove_member", "You can only remove members with a lower role than yours.");

    public static readonly Error CannotChangeRoles = Error.Forbidden("cannot_change_roles", "Only the owner can change roles.");

    public static readonly Error InvalidRole = Error.Conflict("invalid_role", "The owner role can't be assigned or changed.");

    public static Error MemberLimitReached(int limit) =>
        Error.Conflict("member_limit_reached", $"A conversation can have at most {limit} members.");
}
