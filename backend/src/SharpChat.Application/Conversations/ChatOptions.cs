using System.ComponentModel.DataAnnotations;

namespace SharpChat.Application.Conversations;

public sealed class ChatOptions
{
    public const string SectionName = "Chat";

    /// <summary>Maximum members per conversation, owner included. Caps the fan-out cost of each broadcast.</summary>
    [Range(2, 1000)]
    public int MaxGroupMembers { get; set; } = 100;
}
