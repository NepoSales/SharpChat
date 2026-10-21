using SharpChat.Domain.Auth;

namespace SharpChat.Application.Abstractions;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task<IReadOnlyList<RefreshToken>> GetActiveByFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken);

    void Add(RefreshToken token);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
