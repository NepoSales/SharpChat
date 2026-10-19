using System.Net;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SharpChat.IntegrationTests.Infrastructure;

namespace SharpChat.IntegrationTests.Auth;

[Collection(ApiTestGroup.Name)]
public sealed class AccessTokenTests(SharpChatApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Protected_endpoint_requires_a_token()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetMeAsync(accessToken: null)).StatusCode);
    }

    [Fact]
    public async Task Malformed_token_is_rejected()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetMeAsync("not-a-jwt")).StatusCode);
    }

    [Fact]
    public async Task Expired_token_is_rejected()
    {
        var token = CreateToken(SharpChatApiFactory.JwtSigningKey, expiresAt: DateTime.UtcNow.AddMinutes(-5));

        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetMeAsync(token)).StatusCode);
    }

    [Fact]
    public async Task Token_signed_with_another_key_is_rejected()
    {
        var token = CreateToken("some-other-signing-key-that-is-long-enough-123", expiresAt: DateTime.UtcNow.AddMinutes(5));

        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetMeAsync(token)).StatusCode);
    }

    private static string CreateToken(string signingKey, DateTime expiresAt) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = SharpChatApiFactory.JwtIssuer,
            Audience = SharpChatApiFactory.JwtAudience,
            IssuedAt = expiresAt.AddMinutes(-15),
            NotBefore = expiresAt.AddMinutes(-15),
            Expires = expiresAt,
            Claims = new Dictionary<string, object> { [JwtRegisteredClaimNames.Sub] = Guid.NewGuid().ToString() },
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), SecurityAlgorithms.HmacSha256),
        });
}
