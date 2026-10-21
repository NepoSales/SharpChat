using SharpChat.Application.Users;
using SharpChat.Domain.Common;

namespace SharpChat.Application.Abstractions;

public interface IIdentityService
{
    Task<Result<UserDto>> CreateUserAsync(string email, string userName, string password, CancellationToken cancellationToken);

    Task<UserDto?> ValidateCredentialsAsync(string email, string password, CancellationToken cancellationToken);

    Task<UserDto?> FindByIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<UserDto?> FindByUserNameAsync(string userName, CancellationToken cancellationToken);
    
    Task<IReadOnlyList<UserDto>> FindByUserNamesAsync(IReadOnlyCollection<string> userNames, CancellationToken cancellationToken);
}
