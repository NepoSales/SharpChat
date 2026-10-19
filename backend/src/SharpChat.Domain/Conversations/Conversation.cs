using SharpChat.Domain.Common;

namespace SharpChat.Domain.Conversations;

/// <summary>
/// A group conversation and its members. All membership rules live here; a 1:1 chat is a group with two members.
/// </summary>
public sealed class Conversation
{
    public const int NameMaxLength = 100;

    private readonly List<ConversationMember> _members = [];

    private Conversation()
    {
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Updated on every membership change; also used as the optimistic concurrency boundary.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<ConversationMember> Members => _members.AsReadOnly();

    /// <summary>Creates a conversation with the creator as <see cref="ConversationRole.Owner"/> plus the initial members.</summary>
    public static Result<Conversation> Create(
        string name,
        Guid creatorId,
        IEnumerable<Guid> initialMemberIds,
        int maxMembers,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var conversation = new Conversation
        {
            Id = Guid.CreateVersion7(),
            Name = name.Trim(),
            CreatedBy = creatorId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        conversation._members.Add(new ConversationMember(conversation.Id, creatorId, ConversationRole.Owner, now));

        foreach (var userId in initialMemberIds.Distinct().Where(id => id != creatorId))
        {
            if (conversation._members.Count >= maxMembers)
            {
                return ConversationErrors.MemberLimitReached(maxMembers);
            }

            conversation._members.Add(new ConversationMember(conversation.Id, userId, ConversationRole.Member, now));
        }

        return conversation;
    }

    public ConversationMember? FindMember(Guid userId) => _members.Find(m => m.UserId == userId);

    public bool IsMember(Guid userId) => FindMember(userId) is not null;

    public Result<ConversationMember> AddMember(Guid actorId, Guid userId, int maxMembers, DateTimeOffset now)
    {
        var actor = FindMember(actorId);
        if (actor is null)
        {
            return ConversationErrors.NotFound;
        }

        if (!actor.CanManageMembers)
        {
            return ConversationErrors.CannotManageMembers;
        }

        if (IsMember(userId))
        {
            return ConversationErrors.AlreadyMember;
        }

        if (_members.Count >= maxMembers)
        {
            return ConversationErrors.MemberLimitReached(maxMembers);
        }

        var member = new ConversationMember(Id, userId, ConversationRole.Member, now);
        _members.Add(member);
        UpdatedAt = now;
        return member;
    }

    /// <summary>Removes a member. A member removing themselves is leaving the conversation.</summary>
    public Result RemoveMember(Guid actorId, Guid userId, DateTimeOffset now)
    {
        var actor = FindMember(actorId);
        if (actor is null)
        {
            return ConversationErrors.NotFound;
        }

        var target = FindMember(userId);
        if (target is null)
        {
            return ConversationErrors.MemberNotFound;
        }

        if (target.Role == ConversationRole.Owner)
        {
            return ConversationErrors.OwnerCannotLeave;
        }

        var isLeaving = actorId == userId;
        if (!isLeaving && (!actor.CanManageMembers || actor.Role <= target.Role))
        {
            return ConversationErrors.CannotRemoveMember;
        }

        _members.Remove(target);
        UpdatedAt = now;
        return Result.Success();
    }

    public Result<ConversationMember> ChangeRole(Guid actorId, Guid userId, ConversationRole role, DateTimeOffset now)
    {
        var actor = FindMember(actorId);
        if (actor is null)
        {
            return ConversationErrors.NotFound;
        }

        if (actor.Role != ConversationRole.Owner)
        {
            return ConversationErrors.CannotChangeRoles;
        }

        var target = FindMember(userId);
        if (target is null)
        {
            return ConversationErrors.MemberNotFound;
        }

        if (role == ConversationRole.Owner || target.Role == ConversationRole.Owner)
        {
            return ConversationErrors.InvalidRole;
        }

        target.Role = role;
        UpdatedAt = now;
        return target;
    }
}
