using SharpChat.Application.Users;

namespace SharpChat.Application.Abstractions;

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

public sealed record NewRefreshToken(string Token, string Hash, DateTimeOffset ExpiresAt);

public interface ITokenService
{
    AccessToken CreateAccessToken(UserDto user);

    NewRefreshToken CreateRefreshToken();

    string HashRefreshToken(string token);
}
