using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.JsonWebTokens;

namespace SharpChat.Api.Hubs;

internal static class HubAuthentication
{
    public static IServiceCollection AddHubAuthentication(this IServiceCollection services)
    {
        services.AddSingleton<IUserIdProvider, SubClaimUserIdProvider>();

        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.Events ??= new JwtBearerEvents();
            options.Events.OnMessageReceived = context =>
            {
                var token = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = token;
                }

                return Task.CompletedTask;
            };
        });

        return services;
    }

    private sealed class SubClaimUserIdProvider : IUserIdProvider
    {
        public string? GetUserId(HubConnectionContext connection) =>
            connection.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
    }
}
