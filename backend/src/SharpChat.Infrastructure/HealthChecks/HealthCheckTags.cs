namespace SharpChat.Infrastructure.HealthChecks;

public static class HealthCheckTags
{
    /// <summary>Checks that must pass before the instance receives traffic.</summary>
    public const string Ready = "ready";
}
