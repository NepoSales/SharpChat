namespace SharpChat.Application.Abstractions;

public interface ICurrentUser
{
    Guid? UserId { get; }
}
