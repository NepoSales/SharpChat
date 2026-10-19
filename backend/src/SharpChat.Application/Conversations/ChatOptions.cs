using System.ComponentModel.DataAnnotations;

namespace SharpChat.Application.Conversations;

public sealed class ChatOptions
{
    public const string SectionName = "Chat";

    [Range(2, 1000)]
    public int MaxGroupMembers { get; set; } = 100;

    [Range(1, 10_000)]
    public int MaxMessageLength { get; set; } = 4000;

    [Range(1, 100)]
    public int DefaultHistoryPageSize { get; set; } = 50;

    public const int MaxHistoryPageSize = 100;
}
