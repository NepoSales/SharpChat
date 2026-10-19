using FluentValidation;
using Microsoft.Extensions.Logging;
using SharpChat.Application.Abstractions;
using SharpChat.Application.Common;
using SharpChat.Application.Users;
using SharpChat.Domain.Auth;

namespace SharpChat.Application.Auth;

public sealed partial class AuthService(
    IIdentityService identity,
    ITokenService tokens,
    IRefreshTokenRepository refreshTokens,
    IValidator<RegisterRequest> registerValidator,
    IValidator<LoginRequest> loginValidator,
    TimeProvider time,
    ILogger<AuthService> logger)
{
    private static readonly Error InvalidCredentials =
        Error.Unauthorized("invalid_credentials", "Email or password is incorrect.");

    private static readonly Error InvalidRefreshToken =
        Error.Unauthorized("invalid_refresh_token", "The refresh token is invalid or expired.");

    public async Task<Result<UserDto>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var validation = await registerValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToError();
        }

        return await identity.CreateUserAsync(request.Email, request.UserName, request.Password, cancellationToken);
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var validation = await loginValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToError();
        }

        var user = await identity.ValidateCredentialsAsync(request.Email, request.Password, cancellationToken);
        if (user is null)
        {
            return InvalidCredentials;
        }

        // A login starts a new token family (one per session/device).
        var response = IssueTokens(user, familyId: Guid.CreateVersion7(), out _);
        await refreshTokens.SaveChangesAsync(cancellationToken);
        return response;
    }

    public async Task<Result<AuthResponse>> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return InvalidRefreshToken;
        }

        var stored = await refreshTokens.GetByHashAsync(tokens.HashRefreshToken(request.RefreshToken), cancellationToken);
        if (stored is null)
        {
            return InvalidRefreshToken;
        }

        var now = time.GetUtcNow();

        if (stored.IsRevoked)
        {
            // A rotated token was used again: it may have been stolen, so end the whole session.
            LogRefreshTokenReuse(stored.UserId, stored.FamilyId);
            await RevokeFamilyAsync(stored.FamilyId, now, cancellationToken);
            return InvalidRefreshToken;
        }

        if (stored.IsExpired(now))
        {
            return InvalidRefreshToken;
        }

        var user = await identity.FindByIdAsync(stored.UserId, cancellationToken);
        if (user is null)
        {
            return InvalidRefreshToken;
        }

        var response = IssueTokens(user, stored.FamilyId, out var replacement);
        stored.Revoke(now, replacement.Id);

        try
        {
            await refreshTokens.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // Another request rotated this token at the same time; only one of them wins.
            return InvalidRefreshToken;
        }

        return response;
    }

    /// <summary>Ends the session the refresh token belongs to. Unknown tokens are ignored, so logout is idempotent.</summary>
    public async Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return;
        }

        var stored = await refreshTokens.GetByHashAsync(tokens.HashRefreshToken(request.RefreshToken), cancellationToken);
        if (stored is not null)
        {
            await RevokeFamilyAsync(stored.FamilyId, time.GetUtcNow(), cancellationToken);
        }
    }

    private AuthResponse IssueTokens(UserDto user, Guid familyId, out RefreshToken refreshToken)
    {
        var accessToken = tokens.CreateAccessToken(user);
        var newRefreshToken = tokens.CreateRefreshToken();

        refreshToken = RefreshToken.Issue(user.Id, newRefreshToken.Hash, familyId, time.GetUtcNow(), newRefreshToken.ExpiresAt);
        refreshTokens.Add(refreshToken);

        return new AuthResponse(accessToken.Token, accessToken.ExpiresAt, newRefreshToken.Token, newRefreshToken.ExpiresAt);
    }

    private async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var token in await refreshTokens.GetActiveByFamilyAsync(familyId, now, cancellationToken))
        {
            token.Revoke(now);
        }

        try
        {
            await refreshTokens.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // A concurrent request changed a token of this family; it is being revoked or rotated already.
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refresh token reuse detected for user {UserId}; revoking token family {FamilyId}")]
    private partial void LogRefreshTokenReuse(Guid userId, Guid familyId);
}
