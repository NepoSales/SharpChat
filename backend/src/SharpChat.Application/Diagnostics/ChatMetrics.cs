using System.Diagnostics.Metrics;

namespace SharpChat.Application.Diagnostics;

public enum SendOutcome
{
    Created,
    Duplicate,
    Rejected,
}

public enum MessageChange
{
    Edited,
    Deleted,
}

public sealed class ChatMetrics
{
    public const string MeterName = "SharpChat";

    private static readonly double[] SendDurationBuckets =
        [0.001, 0.0025, 0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 1, 2.5];

    private readonly Counter<long> _messagesSent;
    private readonly Histogram<double> _sendDuration;
    private readonly Counter<long> _messagesChanged;
    private readonly Counter<long> _presenceTransitions;
    private readonly Counter<long> _hubRateLimited;

    public ChatMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _messagesSent = meter.CreateCounter<long>(
            "sharpchat.messages.sent", "{message}", "New messages stored and broadcast (retries of the same id are not counted).");
        _sendDuration = meter.CreateHistogram(
            "sharpchat.messages.send.duration",
            "s",
            "Time to handle a send: membership check, sequenced write and broadcast.",
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = SendDurationBuckets });
        _messagesChanged = meter.CreateCounter<long>(
            "sharpchat.messages.changed", "{message}", "Messages edited or deleted.");
        _presenceTransitions = meter.CreateCounter<long>(
            "sharpchat.presence.transitions", "{transition}", "Users going online (first connection) or offline (last connection).");
        _hubRateLimited = meter.CreateCounter<long>(
            "sharpchat.hub.rate_limited", "{invocation}", "Hub invocations rejected by the rate limiter.");
    }

    public void MessageSent(bool isReply) =>
        _messagesSent.Add(1, new KeyValuePair<string, object?>("sharpchat.message.kind", isReply ? "reply" : "message"));

    public void SendCompleted(SendOutcome outcome, TimeSpan elapsed) =>
        _sendDuration.Record(
            elapsed.TotalSeconds,
            new KeyValuePair<string, object?>("sharpchat.send.outcome", outcome.ToString().ToLowerInvariant()));

    public void MessageChanged(MessageChange change) =>
        _messagesChanged.Add(1, new KeyValuePair<string, object?>("sharpchat.message.change", change.ToString().ToLowerInvariant()));

    public void PresenceChanged(bool isOnline) =>
        _presenceTransitions.Add(1, new KeyValuePair<string, object?>("sharpchat.presence.state", isOnline ? "online" : "offline"));

    public void HubRateLimited(string method) =>
        _hubRateLimited.Add(1, new KeyValuePair<string, object?>("sharpchat.hub.method", method));
}
