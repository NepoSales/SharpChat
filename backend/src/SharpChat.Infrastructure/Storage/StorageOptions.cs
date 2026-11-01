using System.ComponentModel.DataAnnotations;

namespace SharpChat.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    [Required]
    public string RootPath { get; set; } = "data/attachments";

    [Range(typeof(TimeSpan), "00:00:01", "1.00:00:00")]
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(10);

    public bool CleanupEnabled { get; set; } = true;
}
