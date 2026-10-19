using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using SharpChat.Infrastructure.HealthChecks;
using SharpChat.Infrastructure.Persistence;
using StackExchange.Redis;

namespace SharpChat.Infrastructure;

public static class DependencyInjection
{
    public const string ReadyTag = "ready";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var postgres = RequiredConnectionString(configuration, "Postgres");
        var redis = RequiredConnectionString(configuration, "Redis");

        services.AddDbContext<SharpChatDbContext>(options => options.UseNpgsql(postgres));

        // AbortOnConnectFail=false lets the API start while Redis is down; the readiness check reports it.
        var redisOptions = ConfigurationOptions.Parse(redis);
        redisOptions.AbortOnConnectFail = false;
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisOptions));

        services.AddHealthChecks()
            .AddDbContextCheck<SharpChatDbContext>("postgres", tags: [ReadyTag])
            .AddCheck<RedisHealthCheck>("redis", tags: [ReadyTag]);

        return services;
    }

    private static string RequiredConnectionString(IConfiguration configuration, string name)
    {
        var value = configuration.GetConnectionString(name);
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Connection string '{name}' is missing.")
            : value;
    }
}
