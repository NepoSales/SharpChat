using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SharpChat.Application.Auth;
using SharpChat.Application.Conversations;
using SharpChat.Application.Messages;
using SharpChat.Application.Presence;
using SharpChat.Application.Users;

namespace SharpChat.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        services.AddScoped<MembershipService>();
        services.AddScoped<ConversationService>();
        services.AddScoped<MessageService>();
        services.AddScoped<ReceiptService>();
        services.AddScoped<PresenceService>();
        services.AddScoped<TypingService>();

        return services;
    }
}
