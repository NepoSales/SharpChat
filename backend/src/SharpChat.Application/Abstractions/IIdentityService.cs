using SharpChat.Application.Common;
using SharpChat.Application.Users;

namespace SharpChat.Application.Abstractions;

public interface IIdentityService
{
    Task<Result<UserDto>> CreateUserAsync(string email, string userName, string password, CancellationToken cancellationToken);

    /// <summary>Returns the user when the credentials are valid and the account is not locked out; otherwise null.</summary>
    Task<UserDto?> ValidateCredentialsAsync(string email, string password, CancellationToken cancellationToken);

    Task<UserDto?> FindByIdAsync(Guid userId, CancellationToken cancellationToken);
}
