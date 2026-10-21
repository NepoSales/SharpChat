using System.ComponentModel.DataAnnotations;

namespace SharpChat.Api.RateLimiting;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    [Required]
    public string KeyPrefix { get; set; } = "sharpchat:ratelimit";

    public RateLimitRule Auth { get; set; } = new(10, 60);

    public RateLimitRule Api { get; set; } = new(300, 60);

    public RateLimitRule HubMessages { get; set; } = new(30, 10);

    public RateLimitRule Typing { get; set; } = new(2, 4);
}

public sealed class RateLimitRule
{
    public RateLimitRule()
    {
    }

    public RateLimitRule(int permitLimit, int windowSeconds)
    {
        PermitLimit = permitLimit;
        WindowSeconds = windowSeconds;
    }

    [Range(1, int.MaxValue)]
    public int PermitLimit { get; set; }

    [Range(1, 86_400)]
    public int WindowSeconds { get; set; }

    public TimeSpan Window => TimeSpan.FromSeconds(WindowSeconds);
}
