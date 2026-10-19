using SharpChat.Application.Users;

namespace SharpChat.Application.Abstractions;

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

/// <summary>A new refresh token: the raw value goes to the client, only the hash is stored.</summary>
public sealed record NewRefreshToken(string Token, string Hash, DateTimeOffset ExpiresAt);

public interface ITokenService
{
    AccessToken CreateAccessToken(UserDto user);

    NewRefreshToken CreateRefreshToken();

    string HashRefreshToken(string token);
}
