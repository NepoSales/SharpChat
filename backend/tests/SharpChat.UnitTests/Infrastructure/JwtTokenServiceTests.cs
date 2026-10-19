using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SharpChat.Application.Users;
using SharpChat.Infrastructure.Authentication;

namespace SharpChat.UnitTests.Infrastructure;

public sealed class JwtTokenServiceTests
{
    private static readonly UserDto User = new(Guid.NewGuid(), "ada", "ada@example.com");

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly JwtOptions _options = new()
    {
        Issuer = "sharpchat-tests",
        Audience = "sharpchat-tests",
        SigningKey = new string('k', 48),
        AccessTokenLifetime = TimeSpan.FromMinutes(15),
        RefreshTokenLifetime = TimeSpan.FromDays(14),
    };

    private JwtTokenService CreateService() => new(Options.Create(_options), _time);

    [Fact]
    public async Task Access_token_carries_the_user_and_is_signed_with_the_configured_key()
    {
        var accessToken = CreateService().CreateAccessToken(User);

        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(accessToken.Token, new TokenValidationParameters
        {
            ValidIssuer = _options.Issuer,
            ValidAudience = _options.Audience,
            IssuerSigningKey = _options.CreateSigningKey(),
            ValidateLifetime = false,
        });

        Assert.True(validation.IsValid);
        Assert.Equal(User.Id.ToString(), validation.Claims[JwtRegisteredClaimNames.Sub]);
        Assert.Equal(User.UserName, validation.Claims[JwtRegisteredClaimNames.Name]);
        Assert.Equal(_time.GetUtcNow().AddMinutes(15), accessToken.ExpiresAt);
    }

    [Fact]
    public void Refresh_tokens_are_unique_and_only_their_hash_is_kept()
    {
        var service = CreateService();

        var first = service.CreateRefreshToken();
        var second = service.CreateRefreshToken();

        Assert.NotEqual(first.Token, second.Token);
        Assert.NotEqual(first.Token, first.Hash);
        Assert.Equal(service.HashRefreshToken(first.Token), first.Hash);
        Assert.Equal(_time.GetUtcNow().AddDays(14), first.ExpiresAt);
    }
}
