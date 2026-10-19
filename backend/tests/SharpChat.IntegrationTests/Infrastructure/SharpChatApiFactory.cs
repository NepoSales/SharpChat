using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace SharpChat.IntegrationTests.Infrastructure;

/// <summary>
/// Runs the API in memory against real Postgres and Redis containers, shared by every test in the collection.
/// </summary>
public sealed class SharpChatApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string JwtIssuer = "sharpchat";
    public const string JwtAudience = "sharpchat";
    public const string JwtSigningKey = "integration-tests-signing-key-not-a-secret-0123456789";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
        builder.UseSetting("ConnectionStrings:Redis", _redis.GetConnectionString());
        builder.UseSetting("Jwt:SigningKey", JwtSigningKey);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _redis.DisposeAsync().AsTask());
    }
}

[CollectionDefinition(Name)]
public sealed class ApiTestGroup : ICollectionFixture<SharpChatApiFactory>
{
    public const string Name = "Api";
}
