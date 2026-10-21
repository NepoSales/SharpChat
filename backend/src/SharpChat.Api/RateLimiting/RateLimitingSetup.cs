using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using RedisRateLimiting;
using StackExchange.Redis;

namespace SharpChat.Api.RateLimiting;

internal static class RateLimitingSetup
{
    public const string AuthPolicy = "auth";

    public static IServiceCollection AddSharpChatRateLimiting(this IServiceCollection services)
    {
        services.AddOptions<RateLimitingOptions>()
            .BindConfiguration(RateLimitingOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = WriteRejectionAsync;
        });

        services.AddOptions<RateLimiterOptions>()
            .Configure<IConnectionMultiplexer, IOptions<RateLimitingOptions>>((options, redis, settings) =>
            {
                var limits = settings.Value;

                options.AddPolicy(AuthPolicy, context =>
                    Partition(redis, $"{limits.KeyPrefix}:auth:{ClientAddress(context)}", limits.Auth));

                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    Partition(redis, $"{limits.KeyPrefix}:api:{Caller(context)}", limits.Api));
            });

        services.AddSingleton<HubRateLimiters>();

        return services;
    }

    public static RateLimitPartition<string> Partition(IConnectionMultiplexer redis, string key, RateLimitRule rule) =>
        RedisRateLimitPartition.GetFixedWindowRateLimiter(key, _ => new RedisFixedWindowRateLimiterOptions
        {
            ConnectionMultiplexerFactory = () => redis,
            PermitLimit = rule.PermitLimit,
            Window = rule.Window,
        });

    private static string Caller(HttpContext context) =>
        context.User.FindFirstValue(JwtRegisteredClaimNames.Sub) is { } userId ? $"user:{userId}" : $"ip:{ClientAddress(context)}";

    private static string ClientAddress(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? IPAddress.None.ToString();

    private static int? RetryAfterSeconds(RateLimitLease lease)
    {
        if (lease.TryGetMetadata(RateLimitMetadataName.RetryAfter, out var redisSeconds))
        {
            return Math.Max(redisSeconds, 1);
        }

        return lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            ? Math.Max((int)Math.Ceiling(retryAfter.TotalSeconds), 1)
            : null;
    }

    private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        if (RetryAfterSeconds(context.Lease) is { } seconds)
        {
            context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        }

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many requests",
            Detail = "You are sending requests too fast. Please wait a moment and try again.",
        };
        problem.Extensions["code"] = "rate_limited";

        var problems = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problems.WriteAsync(new ProblemDetailsContext { HttpContext = context.HttpContext, ProblemDetails = problem });
    }
}
