using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SharpChat.Application.Auth;
using SharpChat.Application.Users;

namespace SharpChat.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();

        return services;
    }
}
