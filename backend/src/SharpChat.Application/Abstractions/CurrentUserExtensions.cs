namespace SharpChat.Application.Abstractions;

public static class CurrentUserExtensions
{
    public static Guid RequiredUserId(this ICurrentUser currentUser) =>
        currentUser.UserId ?? throw new InvalidOperationException("The request has no authenticated user.");
}
