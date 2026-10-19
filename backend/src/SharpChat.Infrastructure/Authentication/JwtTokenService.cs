using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SharpChat.Application.Abstractions;
using SharpChat.Application.Users;

namespace SharpChat.Infrastructure.Authentication;

internal sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider time) : ITokenService
{
    private const int RefreshTokenBytes = 64;

    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken CreateAccessToken(UserDto user)
    {
        var jwt = options.Value;
        var now = time.GetUtcNow();
        var expiresAt = now + jwt.AccessTokenLifetime;

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
                [JwtRegisteredClaimNames.Name] = user.UserName,
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
            },
            SigningCredentials = new SigningCredentials(jwt.CreateSigningKey(), SecurityAlgorithms.HmacSha256),
        });

        return new AccessToken(token, expiresAt);
    }

    public NewRefreshToken CreateRefreshToken()
    {
        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(RefreshTokenBytes));
        return new NewRefreshToken(token, HashRefreshToken(token), time.GetUtcNow() + options.Value.RefreshTokenLifetime);
    }

    public string HashRefreshToken(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
