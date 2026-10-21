using Microsoft.AspNetCore.SignalR;
using SharpChat.Api.RateLimiting;
using SharpChat.Application.Diagnostics;

namespace SharpChat.Api.Hubs;

internal sealed class HubRateLimitFilter(HubRateLimiters limiters, ChatMetrics metrics) : IHubFilter
{
    private static readonly HashSet<string> MessageMethods =
        [nameof(ChatHub.SendMessage), nameof(ChatHub.EditMessage), nameof(ChatHub.DeleteMessage)];

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var method = invocationContext.HubMethodName;
        var limiter = method == nameof(ChatHub.Typing)
            ? limiters.Typing
            : MessageMethods.Contains(method) ? limiters.Messages : null;
        if (limiter is null)
        {
            return await next(invocationContext);
        }

        var caller = invocationContext.Context.UserIdentifier ?? invocationContext.Context.ConnectionId;
        using var lease = await limiter.AcquireAsync(caller, 1, invocationContext.Context.ConnectionAborted);
        if (lease.IsAcquired)
        {
            return await next(invocationContext);
        }

        metrics.HubRateLimited(method);
        return method == nameof(ChatHub.Typing)
            ? null
            : HubResponse<object>.Fail("rate_limited", "You are sending messages too fast. Please wait a moment.");
    }
}
