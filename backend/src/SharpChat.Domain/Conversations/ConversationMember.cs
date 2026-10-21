namespace SharpChat.Domain.Conversations;

public sealed class ConversationMember
{
    private ConversationMember()
    {
    }

    internal ConversationMember(Guid conversationId, Guid userId, ConversationRole role, DateTimeOffset joinedAt)
    {
        ConversationId = conversationId;
        UserId = userId;
        Role = role;
        JoinedAt = joinedAt;
    }

    public Guid ConversationId { get; private set; }
    public Guid UserId { get; private set; }
    public ConversationRole Role { get; internal set; }
    public DateTimeOffset JoinedAt { get; private set; }

    public long LastDeliveredSeq { get; private set; }
    public long LastReadSeq { get; private set; }

    public ReceiptPosition Receipt => new(LastDeliveredSeq, LastReadSeq);

    public bool CanManageMembers => Role >= ConversationRole.Admin;
}
