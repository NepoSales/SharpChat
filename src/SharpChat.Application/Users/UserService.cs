using SharpChat.Application.Abstractions;
using SharpChat.Application.Common;

namespace SharpChat.Application.Users;

public sealed class UserService(ICurrentUser currentUser, IIdentityService identity)
{
    public async Task<Result<UserDto>> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var user = currentUser.UserId is { } userId
            ? await identity.FindByIdAsync(userId, cancellationToken)
            : null;

        return user is null
            ? Error.NotFound("user_not_found", "The current user no longer exists.")
            : user;
    }
}
