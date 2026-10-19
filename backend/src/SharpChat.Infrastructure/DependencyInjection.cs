using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SharpChat.Application.Abstractions;
using SharpChat.Application.Auth;
using SharpChat.Application.Conversations;
using SharpChat.Infrastructure.Authentication;
using SharpChat.Infrastructure.HealthChecks;
using SharpChat.Infrastructure.Identity;
using SharpChat.Infrastructure.Persistence;
using SharpChat.Infrastructure.Persistence.Queries;
using SharpChat.Infrastructure.Persistence.Repositories;
using StackExchange.Redis;

namespace SharpChat.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var postgres = RequiredConnectionString(configuration, "Postgres");
        var redis = RequiredConnectionString(configuration, "Redis");

        services.TryAddSingleton(TimeProvider.System);

        services.AddDbContext<SharpChatDbContext>(options => options.UseNpgsql(postgres));

        // AbortOnConnectFail=false lets the API start while Redis is down; the readiness check reports it.
        var redisOptions = ConfigurationOptions.Parse(redis);
        redisOptions.AbortOnConnectFail = false;
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisOptions));

        services.AddHealthChecks()
            .AddDbContextCheck<SharpChatDbContext>("postgres", tags: [HealthCheckTags.Ready])
            .AddCheck<RedisHealthCheck>("redis", tags: [HealthCheckTags.Ready]);

        services.AddIdentityAndJwt();

        services.AddOptions<ChatOptions>()
            .BindConfiguration(ChatOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IConversationQueries, ConversationQueries>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        return services;
    }

    private static void AddIdentityAndJwt(this IServiceCollection services)
    {
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.User.AllowedUserNameCharacters = RegisterRequestValidator.UserNameAllowedCharacters;

                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddEntityFrameworkStores<SharpChatDbContext>();

        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // Configured through options so the key is read (and validated) at startup, not at registration.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;

                // Keep claim names as issued ("sub", "name") instead of mapping them to long XML claim types.
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = jwt.CreateSigningKey(),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = JwtRegisteredClaimNames.Name,
                };
            });
    }

    private static string RequiredConnectionString(IConfiguration configuration, string name)
    {
        var value = configuration.GetConnectionString(name);
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Connection string '{name}' is missing.")
            : value;
    }
}
