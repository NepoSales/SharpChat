using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR;
using SharpChat.Application.Abstractions;
using StackExchange.Redis;

namespace SharpChat.Api.Hubs;

internal static class RealtimeServices
{
    public static IServiceCollection AddRealtime(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSignalR(options => options.AddFilter<HubRateLimitFilter>())
            .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .AddStackExchangeRedis(configuration.GetConnectionString("Redis")!, options =>
            {
                options.Configuration.ChannelPrefix = RedisChannel.Literal("sharpchat");
                options.Configuration.AbortOnConnectFail = false;
            });

        services.AddSingleton<ConnectionRegistry>();
        services.AddHostedService<PresenceHeartbeat>();
        services.AddScoped<IChatNotifier, SignalRChatNotifier>();
        services.AddHubAuthentication();

        return services;
    }
}
