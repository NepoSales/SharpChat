using System.ComponentModel.DataAnnotations;

namespace SharpChat.Application.Presence;

public sealed class PresenceOptions
{
    public const string SectionName = "Presence";

    [Range(5, 3600)]
    public int ConnectionTtlSeconds { get; set; } = 60;

    [Range(1, 1200)]
    public int HeartbeatSeconds { get; set; } = 20;
}
