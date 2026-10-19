namespace SharpChat.Application.Abstractions;

/// <summary>The authenticated user of the current request, read from the token claims (never from client input).</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
}
