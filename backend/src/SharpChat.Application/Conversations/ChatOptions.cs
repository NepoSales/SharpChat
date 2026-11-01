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

    [Range(1, MaxAttachmentSizeLimit)]
    public long MaxAttachmentBytes { get; set; } = 10 * 1024 * 1024;

    [Range(1, 10)]
    public int MaxAttachmentsPerMessage { get; set; } = 4;

    [Range(typeof(TimeSpan), "00:01:00", "7.00:00:00")]
    public TimeSpan PendingAttachmentLifetime { get; set; } = TimeSpan.FromHours(24);

    public const int MaxAttachmentSizeLimit = 25 * 1024 * 1024;

    public const int MaxHistoryPageSize = 100;

    public const int MaxChangesPageSize = 200;

    public const int UnreadCountCap = 100;
}
