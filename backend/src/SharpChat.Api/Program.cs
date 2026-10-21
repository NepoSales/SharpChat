using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using SharpChat.Api.Hubs;
using SharpChat.Api.OpenApi;
using SharpChat.Api.Services;
using SharpChat.Application;
using SharpChat.Application.Abstractions;
using SharpChat.Infrastructure;
using SharpChat.Infrastructure.HealthChecks;
using SharpChat.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSignalR()
    .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddApiDocumentation();
builder.Services.AddProblemDetails();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IChatNotifier, SignalRChatNotifier>();
builder.Services.AddHubAuthentication();

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapApiDocumentation();

    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<SharpChatDbContext>().Database.MigrateAsync();
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>(ChatHub.Path);

app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains(HealthCheckTags.Ready),
}).AllowAnonymous();

await app.RunAsync();
