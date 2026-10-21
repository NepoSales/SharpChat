using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SharpChat.Application.Abstractions;
using SharpChat.Application.Users;
using SharpChat.Domain.Common;

namespace SharpChat.Infrastructure.Identity;

internal sealed class IdentityService(UserManager<ApplicationUser> userManager, TimeProvider time) : IIdentityService
{
    private static readonly Error DuplicateEmail = Error.Conflict("email_taken", "This email is already registered.");
    private static readonly Error DuplicateUserName = Error.Conflict("username_taken", "This username is already taken.");

    public async Task<Result<UserDto>> CreateUserAsync(string email, string userName, string password, CancellationToken cancellationToken)
    {
        var user = new ApplicationUser { Email = email, UserName = userName, CreatedAt = time.GetUtcNow() };

        IdentityResult result;
        try
        {
            result = await userManager.CreateAsync(user, password);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } unique)
        {
            return unique.ConstraintName?.Contains("Email", StringComparison.OrdinalIgnoreCase) == true
                ? DuplicateEmail
                : DuplicateUserName;
        }

        return result.Succeeded ? ToDto(user) : ToError(result.Errors);
    }

    public async Task<UserDto?> ValidateCredentialsAsync(string email, string password, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            userManager.PasswordHasher.HashPassword(new ApplicationUser(), password);
            return null;
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return null;
        }

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            await userManager.AccessFailedAsync(user);
            return null;
        }

        await userManager.ResetAccessFailedCountAsync(user);
        return ToDto(user);
    }

    public async Task<UserDto?> FindByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        return user is null ? null : ToDto(user);
    }

    public async Task<UserDto?> FindByUserNameAsync(string userName, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByNameAsync(userName);
        return user is null ? null : ToDto(user);
    }

    public async Task<IReadOnlyList<UserDto>> FindByUserNamesAsync(IReadOnlyCollection<string> userNames, CancellationToken cancellationToken)
    {
        if (userNames.Count == 0)
        {
            return [];
        }

        var normalized = userNames.Select(userManager.NormalizeName).ToList();
        return await userManager.Users
            .Where(u => normalized.Contains(u.NormalizedUserName))
            .Select(u => new UserDto(u.Id, u.UserName!, u.Email!))
            .ToListAsync(cancellationToken);
    }

    private static UserDto ToDto(ApplicationUser user) => new(user.Id, user.UserName!, user.Email!);

    private static Error ToError(IEnumerable<IdentityError> errors)
    {
        var list = errors.ToList();

        if (list.Any(e => e.Code == nameof(IdentityErrorDescriber.DuplicateEmail)))
        {
            return DuplicateEmail;
        }

        if (list.Any(e => e.Code == nameof(IdentityErrorDescriber.DuplicateUserName)))
        {
            return DuplicateUserName;
        }

        return Error.Validation(list
            .GroupBy(e => FieldFor(e.Code))
            .ToDictionary(group => group.Key, group => group.Select(e => e.Description).ToArray()));
    }

    private static string FieldFor(string identityErrorCode) => identityErrorCode switch
    {
        _ when identityErrorCode.StartsWith("Password", StringComparison.Ordinal) => "Password",
        _ when identityErrorCode.Contains("Email", StringComparison.Ordinal) => "Email",
        _ when identityErrorCode.Contains("UserName", StringComparison.Ordinal) => "UserName",
        _ => string.Empty,
    };
}
