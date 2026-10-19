using SharpChat.Application.Users;
using SharpChat.Domain.Common;

namespace SharpChat.Application.Abstractions;

public interface IIdentityService
{
    Task<Result<UserDto>> CreateUserAsync(string email, string userName, string password, CancellationToken cancellationToken);

    /// <summary>Returns the user when the credentials are valid and the account is not locked out; otherwise null.</summary>
    Task<UserDto?> ValidateCredentialsAsync(string email, string password, CancellationToken cancellationToken);

    Task<UserDto?> FindByIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<UserDto?> FindByUserNameAsync(string userName, CancellationToken cancellationToken);

    /// <summary>Finds users by username (case-insensitive). Unknown names are left out of the result.</summary>
    Task<IReadOnlyList<UserDto>> FindByUserNamesAsync(IReadOnlyCollection<string> userNames, CancellationToken cancellationToken);
}
