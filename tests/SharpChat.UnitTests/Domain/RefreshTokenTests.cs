using SharpChat.Domain.Auth;

namespace SharpChat.UnitTests.Domain;

public sealed class RefreshTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Issue_creates_an_active_token()
    {
        var token = RefreshToken.Issue(Guid.NewGuid(), "hash", Guid.NewGuid(), Now, Now.AddDays(1));

        Assert.True(token.IsActive(Now));
        Assert.False(token.IsRevoked);
    }

    [Fact]
    public void Issue_rejects_an_expiry_in_the_past()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RefreshToken.Issue(Guid.NewGuid(), "hash", Guid.NewGuid(), Now, Now));
    }

    [Fact]
    public void Token_is_inactive_once_expired()
    {
        var token = RefreshToken.Issue(Guid.NewGuid(), "hash", Guid.NewGuid(), Now, Now.AddMinutes(10));

        Assert.True(token.IsExpired(Now.AddMinutes(10)));
        Assert.False(token.IsActive(Now.AddMinutes(10)));
    }

    [Fact]
    public void Revoke_keeps_the_first_revocation()
    {
        var token = RefreshToken.Issue(Guid.NewGuid(), "hash", Guid.NewGuid(), Now, Now.AddDays(1));
        var replacement = Guid.NewGuid();

        token.Revoke(Now, replacement);
        token.Revoke(Now.AddMinutes(5));

        Assert.False(token.IsActive(Now));
        Assert.Equal(Now, token.RevokedAt);
        Assert.Equal(replacement, token.ReplacedByTokenId);
    }
}
