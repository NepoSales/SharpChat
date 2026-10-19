using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SharpChat.Application.Abstractions;
using SharpChat.Application.Auth;
using SharpChat.Application.Common;
using SharpChat.Application.Users;
using SharpChat.Domain.Auth;

namespace SharpChat.UnitTests.Application;

public sealed class AuthServiceTests
{
    private static readonly UserDto User = new(Guid.NewGuid(), "ada", "ada@example.com");

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly IIdentityService _identity = Substitute.For<IIdentityService>();
    private readonly ITokenService _tokens = Substitute.For<ITokenService>();
    private readonly IRefreshTokenRepository _repository = Substitute.For<IRefreshTokenRepository>();
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        _tokens.HashRefreshToken(Arg.Any<string>()).Returns(call => $"hash:{call.Arg<string>()}");
        _tokens.CreateAccessToken(Arg.Any<UserDto>()).Returns(new AccessToken("access", _time.GetUtcNow().AddMinutes(15)));
        _tokens.CreateRefreshToken().Returns(new NewRefreshToken("new", "hash:new", _time.GetUtcNow().AddDays(14)));
        _identity.FindByIdAsync(User.Id, Arg.Any<CancellationToken>()).Returns(User);

        _service = new AuthService(
            _identity,
            _tokens,
            _repository,
            new RegisterRequestValidator(),
            new LoginRequestValidator(),
            _time,
            NullLogger<AuthService>.Instance);
    }

    [Fact]
    public async Task Refresh_rotates_the_token_within_the_same_family()
    {
        var stored = StoreToken("old");
        RefreshToken? added = null;
        _repository.Add(Arg.Do<RefreshToken>(token => added = token));

        var result = await _service.RefreshAsync(new RefreshRequest("old"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("new", result.Value.RefreshToken);
        Assert.NotNull(added);
        Assert.Equal(stored.FamilyId, added.FamilyId);
        Assert.True(stored.IsRevoked);
        Assert.Equal(added.Id, stored.ReplacedByTokenId);
    }

    [Fact]
    public async Task Refresh_with_a_rotated_token_revokes_the_whole_family()
    {
        var reused = StoreToken("old");
        reused.Revoke(_time.GetUtcNow());
        var current = RefreshToken.Issue(User.Id, "hash:current", reused.FamilyId, _time.GetUtcNow(), _time.GetUtcNow().AddDays(1));
        _repository.GetActiveByFamilyAsync(reused.FamilyId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([current]);

        var result = await _service.RefreshAsync(new RefreshRequest("old"), CancellationToken.None);

        Assert.Equal(ErrorType.Unauthorized, result.Error?.Type);
        Assert.True(current.IsRevoked);
        _tokens.DidNotReceive().CreateAccessToken(Arg.Any<UserDto>());
    }

    [Fact]
    public async Task Refresh_with_an_expired_token_fails()
    {
        StoreToken("old");
        _time.Advance(TimeSpan.FromDays(2));

        var result = await _service.RefreshAsync(new RefreshRequest("old"), CancellationToken.None);

        Assert.Equal("invalid_refresh_token", result.Error?.Code);
    }

    [Fact]
    public async Task Refresh_with_an_unknown_token_fails()
    {
        var result = await _service.RefreshAsync(new RefreshRequest("unknown"), CancellationToken.None);

        Assert.Equal("invalid_refresh_token", result.Error?.Code);
    }

    [Fact]
    public async Task Refresh_that_loses_a_concurrent_rotation_fails()
    {
        StoreToken("old");
        _repository.SaveChangesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new ConcurrencyConflictException());

        var result = await _service.RefreshAsync(new RefreshRequest("old"), CancellationToken.None);

        Assert.Equal("invalid_refresh_token", result.Error?.Code);
    }

    [Fact]
    public async Task Login_with_invalid_credentials_fails_without_issuing_tokens()
    {
        _identity.ValidateCredentialsAsync("ada@example.com", "wrong", Arg.Any<CancellationToken>()).Returns((UserDto?)null);

        var result = await _service.LoginAsync(new LoginRequest("ada@example.com", "wrong"), CancellationToken.None);

        Assert.Equal("invalid_credentials", result.Error?.Code);
        _repository.DidNotReceive().Add(Arg.Any<RefreshToken>());
    }

    private RefreshToken StoreToken(string rawToken)
    {
        var token = RefreshToken.Issue(User.Id, $"hash:{rawToken}", Guid.NewGuid(), _time.GetUtcNow(), _time.GetUtcNow().AddDays(1));
        _repository.GetByHashAsync($"hash:{rawToken}", Arg.Any<CancellationToken>()).Returns(token);
        return token;
    }
}
