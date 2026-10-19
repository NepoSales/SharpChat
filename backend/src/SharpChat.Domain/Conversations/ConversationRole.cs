namespace SharpChat.Domain.Conversations;

/// <summary>Role of a member inside one conversation. Higher values have more permissions.</summary>
public enum ConversationRole
{
    Member = 0,
    Admin = 1,
    Owner = 2,
}
