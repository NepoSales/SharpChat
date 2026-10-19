using SharpChat.Domain.Conversations;

namespace SharpChat.UnitTests.Domain;

public sealed class ConversationTests
{
    private const int MaxMembers = 4;
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly Guid _owner = Guid.NewGuid();
    private readonly Guid _admin = Guid.NewGuid();
    private readonly Guid _member = Guid.NewGuid();
    private readonly Guid _outsider = Guid.NewGuid();

    [Fact]
    public void Creator_becomes_owner_and_initial_members_are_members()
    {
        var conversation = Conversation.Create("  dev team  ", _owner, [_member, _member, _owner], MaxMembers, Now).Value!;

        Assert.Equal("dev team", conversation.Name);
        Assert.Equal(2, conversation.Members.Count);
        Assert.Equal(ConversationRole.Owner, conversation.FindMember(_owner)?.Role);
        Assert.Equal(ConversationRole.Member, conversation.FindMember(_member)?.Role);
    }

    [Fact]
    public void Create_rejects_more_initial_members_than_the_limit()
    {
        var result = Conversation.Create("big", _owner, [.. Enumerable.Range(0, MaxMembers).Select(_ => Guid.NewGuid())], MaxMembers, Now);

        Assert.Equal("member_limit_reached", result.Error?.Code);
    }

    [Fact]
    public void Admin_can_add_members()
    {
        var conversation = CreateWithRoles();
        var newcomer = Guid.NewGuid();

        var result = conversation.AddMember(_admin, newcomer, MaxMembers, Now.AddMinutes(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(ConversationRole.Member, result.Value.Role);
        Assert.Equal(Now.AddMinutes(1), conversation.UpdatedAt);
    }

    [Fact]
    public void Member_cannot_add_members()
    {
        var result = CreateWithRoles().AddMember(_member, Guid.NewGuid(), MaxMembers, Now);

        Assert.Equal("cannot_manage_members", result.Error?.Code);
    }

    [Fact]
    public void Outsider_gets_not_found()
    {
        var conversation = CreateWithRoles();

        Assert.Equal("conversation_not_found", conversation.AddMember(_outsider, Guid.NewGuid(), MaxMembers, Now).Error?.Code);
        Assert.Equal("conversation_not_found", conversation.RemoveMember(_outsider, _member, Now).Error?.Code);
        Assert.Equal("conversation_not_found", conversation.ChangeRole(_outsider, _member, ConversationRole.Admin, Now).Error?.Code);
    }

    [Fact]
    public void Adding_an_existing_member_conflicts()
    {
        var result = CreateWithRoles().AddMember(_owner, _member, MaxMembers, Now);

        Assert.Equal("already_member", result.Error?.Code);
    }

    [Fact]
    public void Adding_beyond_the_limit_is_rejected()
    {
        var conversation = CreateWithRoles();
        conversation.AddMember(_owner, Guid.NewGuid(), MaxMembers, Now);

        var result = conversation.AddMember(_owner, Guid.NewGuid(), MaxMembers, Now);

        Assert.Equal("member_limit_reached", result.Error?.Code);
        Assert.Equal(MaxMembers, conversation.Members.Count);
    }

    [Theory]
    [InlineData(Actor.Owner, Actor.Admin, true)]
    [InlineData(Actor.Owner, Actor.Member, true)]
    [InlineData(Actor.Admin, Actor.Member, true)]
    [InlineData(Actor.Admin, Actor.Admin, true)]
    [InlineData(Actor.Member, Actor.Member, true)]
    [InlineData(Actor.Member, Actor.Admin, false)]
    public void Members_can_only_remove_lower_roles_or_themselves(Actor actor, Actor target, bool allowed)
    {
        var conversation = CreateWithRoles();

        var result = conversation.RemoveMember(IdOf(actor), IdOf(target), Now);

        Assert.Equal(allowed, result.IsSuccess);
        Assert.Equal(!allowed, conversation.IsMember(IdOf(target)));
    }

    [Fact]
    public void Admin_cannot_remove_another_admin()
    {
        var conversation = CreateWithRoles();
        var otherAdmin = Guid.NewGuid();
        conversation.AddMember(_owner, otherAdmin, MaxMembers, Now);
        conversation.ChangeRole(_owner, otherAdmin, ConversationRole.Admin, Now);

        Assert.Equal("cannot_remove_member", conversation.RemoveMember(_admin, otherAdmin, Now).Error?.Code);
    }

    [Fact]
    public void Owner_cannot_leave_or_be_removed()
    {
        var conversation = CreateWithRoles();

        Assert.Equal("owner_cannot_leave", conversation.RemoveMember(_owner, _owner, Now).Error?.Code);
        Assert.Equal("owner_cannot_leave", conversation.RemoveMember(_admin, _owner, Now).Error?.Code);
    }

    [Fact]
    public void Only_the_owner_changes_roles()
    {
        var conversation = CreateWithRoles();

        Assert.Equal("cannot_change_roles", conversation.ChangeRole(_admin, _member, ConversationRole.Admin, Now).Error?.Code);

        var promoted = conversation.ChangeRole(_owner, _member, ConversationRole.Admin, Now);
        Assert.Equal(ConversationRole.Admin, promoted.Value?.Role);
    }

    [Fact]
    public void Owner_role_cannot_be_assigned_or_changed()
    {
        var conversation = CreateWithRoles();

        Assert.Equal("invalid_role", conversation.ChangeRole(_owner, _member, ConversationRole.Owner, Now).Error?.Code);
        Assert.Equal("invalid_role", conversation.ChangeRole(_owner, _owner, ConversationRole.Member, Now).Error?.Code);
    }

    public enum Actor
    {
        Owner,
        Admin,
        Member,
    }

    private Guid IdOf(Actor actor) => actor switch
    {
        Actor.Owner => _owner,
        Actor.Admin => _admin,
        _ => _member,
    };

    private Conversation CreateWithRoles()
    {
        var conversation = Conversation.Create("team", _owner, [_admin, _member], MaxMembers, Now).Value!;
        conversation.ChangeRole(_owner, _admin, ConversationRole.Admin, Now);
        return conversation;
    }
}
