using SharpChat.Application.Auth;

namespace SharpChat.Api.Auth;

public sealed record SessionResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt)
{
    internal static SessionResponse From(AuthResponse tokens) => new(tokens.AccessToken, tokens.AccessTokenExpiresAt);
}
