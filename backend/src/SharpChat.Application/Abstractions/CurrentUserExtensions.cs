namespace SharpChat.Application.Abstractions;

public static class CurrentUserExtensions
{
    /// <summary>The user id of an authenticated request. Endpoints require authentication by default, so it is always set.</summary>
    public static Guid RequiredUserId(this ICurrentUser currentUser) =>
        currentUser.UserId ?? throw new InvalidOperationException("The request has no authenticated user.");
}
